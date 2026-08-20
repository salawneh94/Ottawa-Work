using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace BIMFlow.Shared;

public enum RoomPlanStatus { Valid, Unplaced, NotEnclosed }
public enum KeyPlanCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public record RoomEntry(
    Room Room,
    string Number,
    string Name,
    ElementId LevelId,
    string LevelName,
    string Department,
    double AreaSqFt,
    double PerimeterFeet,
    double VolumeCubicFeet,
    string UpperLimitName,
    double LimitOffsetFeet,
    RoomPlanStatus Status);

public record ViewTypeOptions(
    bool CreateFloorPlan,
    ElementId? FloorPlanViewTemplateId,
    bool AddKeyPlan,
    KeyPlanCorner KeyPlanCorner,
    bool AddElevations,
    bool AddWallSections,
    bool AddCeilingPlan);

public record OutputOptions(
    ElementId TitleBlockTypeId,
    string SheetNumberTemplate,
    string SheetNameTemplate,
    string ViewNameTemplate,
    string FirstSheetNumber,
    string BrowserSortMode, // "None" | "Level" | "Department" | "Custom"
    string SortValue,
    int ScaleDenominator,
    double CropMarginFeet,
    bool CropAnnotationsTight,
    bool AutoFitToSheet,
    bool ShowCropRegion,
    bool OverwriteExisting,
    bool AutoFillSheetParams);

public record RoomPlanResult(int SheetsCreated, int ViewsCreated, int Skipped, List<string> Warnings);

/// <summary>
/// Generates one sheet per selected room, each carrying whichever view
/// types were asked for (cropped floor plan, a key-plan dependent view with
/// the room highlighted, up to 4 interior elevations, one section per
/// boundary wall, a cropped reflected ceiling plan). Sheet/view naming and
/// numbering come from user-editable templates with {Num}/{Name}/{Level}/
/// {Dept} tokens. Viewport placement is a simple non-overlapping grid, not
/// a true auto-layout — same as any batch sheet-building tool, viewports
/// can be dragged afterward in Revit.
/// </summary>
public static class RoomPlanGenerator
{
    private const double SheetMarginFeet = 0.5;

    public static List<RoomEntry> CollectRooms(Document doc)
    {
        var options = new SpatialElementBoundaryOptions();

        return new FilteredElementCollector(doc)
            .OfClass(typeof(SpatialElement))
            .OfType<Room>()
            .Select(room =>
            {
                var placed = room.Location is not null;
                var enclosed = room.Area > 0;
                var status = !placed ? RoomPlanStatus.Unplaced : !enclosed ? RoomPlanStatus.NotEnclosed : RoomPlanStatus.Valid;

                var level = room.LevelId != ElementId.InvalidElementId ? doc.GetElement(room.LevelId) as Level : null;
                var perimeter = room.get_Parameter(BuiltInParameter.ROOM_PERIMETER)?.AsDouble() ?? 0;
                var department = room.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT)?.AsString() ?? "";

                return new RoomEntry(
                    room,
                    room.Number ?? "",
                    room.Name ?? "",
                    room.LevelId,
                    level?.Name ?? "",
                    department,
                    room.Area,
                    perimeter,
                    room.Volume,
                    room.UpperLimit?.Name ?? "",
                    room.LimitOffset,
                    status);
            })
            .OrderBy(r => r.LevelName)
            .ThenBy(r => r.Number)
            .ToList();
    }

    public static string Substitute(string template, RoomEntry room) =>
        template
            .Replace("{Num}", room.Number)
            .Replace("{Name}", room.Name)
            .Replace("{Level}", room.LevelName)
            .Replace("{Dept}", room.Department);

    public static void SaveFinishParameters(Document doc, Room room, string floor, string wall, string ceiling, string baseFinish)
    {
        using var transaction = new Transaction(doc, "BIMFlow: Save Room Finish Parameters");
        transaction.Start();
        SetIfExists(room, BuiltInParameter.ROOM_FINISH_FLOOR, floor);
        SetIfExists(room, BuiltInParameter.ROOM_FINISH_WALL, wall);
        SetIfExists(room, BuiltInParameter.ROOM_FINISH_CEILING, ceiling);
        SetIfExists(room, BuiltInParameter.ROOM_FINISH_BASE, baseFinish);
        transaction.Commit();
    }

    private static void SetIfExists(Room room, BuiltInParameter bip, string value)
    {
        var p = room.get_Parameter(bip);
        if (p is not null && !p.IsReadOnly) p.Set(value);
    }

    public static RoomPlanResult Generate(Document doc, List<RoomEntry> rooms, ViewTypeOptions viewTypes, OutputOptions output)
    {
        var warnings = new List<string>();
        var sheetsCreated = 0;
        var viewsCreated = 0;
        var skipped = 0;

        var floorPlanVft = FirstViewFamilyType(doc, ViewFamily.FloorPlan);
        var ceilingPlanVft = FirstViewFamilyType(doc, ViewFamily.CeilingPlan);
        var elevationVft = FirstViewFamilyType(doc, ViewFamily.Elevation);
        var sectionVft = FirstViewFamilyType(doc, ViewFamily.Section);

        if (viewTypes.CreateFloorPlan && floorPlanVft is null) warnings.Add("No floor plan view type found — floor plans skipped.");
        if (viewTypes.AddCeilingPlan && ceilingPlanVft is null) warnings.Add("No ceiling plan view type found — reflected ceiling plans skipped.");
        if (viewTypes.AddElevations && elevationVft is null) warnings.Add("No elevation view type found — elevations skipped.");
        if (viewTypes.AddWallSections && sectionVft is null) warnings.Add("No section view type found — wall sections skipped.");

        var titleBlockSymbol = doc.GetElement(output.TitleBlockTypeId) as FamilySymbol;
        if (titleBlockSymbol is not null && !titleBlockSymbol.IsActive) titleBlockSymbol.Activate();

        if (output.OverwriteExisting)
            DeletePreviouslyGenerated(doc, rooms, output);

        var existingSheetNames = ExistingViewOrSheetNames(doc, isSheet: true);
        var existingSheetNumbers = ExistingSheetNumbers(doc);
        var existingViewNames = ExistingViewOrSheetNames(doc, isSheet: false);

        for (var i = 0; i < rooms.Count; i++)
        {
            var entry = rooms[i];
            var bbox = entry.Room.get_BoundingBox(null);
            if (bbox is null || entry.Status != RoomPlanStatus.Valid)
            {
                skipped++;
                continue;
            }

            var sheet = ViewSheet.Create(doc, output.TitleBlockTypeId);
            sheet.Name = MakeUnique(existingSheetNames, Substitute(output.SheetNameTemplate, entry));
            sheet.SheetNumber = MakeUnique(
                existingSheetNumbers,
                string.IsNullOrWhiteSpace(output.FirstSheetNumber) ? Substitute(output.SheetNumberTemplate, entry) : IncrementSheetNumber(output.FirstSheetNumber, i));
            sheetsCreated++;

            if (output.AutoFillSheetParams)
            {
                TrySetParameter(sheet, "Room Number", entry.Number);
                TrySetParameter(sheet, "Room Name", entry.Name);
                TrySetParameter(sheet, "Room Level", entry.LevelName);
                TrySetParameter(sheet, "Room Department", entry.Department);
            }

            ApplyBrowserSort(sheet, entry, output);

            var cursor = new XYZ(SheetMarginFeet + 1.0, 2.0, 0);

            if (viewTypes.CreateFloorPlan && floorPlanVft is not null)
            {
                var view = CreateCroppedPlan(doc, floorPlanVft.Id, entry, bbox, output, ViewFamily.FloorPlan);
                view.Name = MakeUnique(existingViewNames, Substitute(output.ViewNameTemplate, entry));
                if (viewTypes.FloorPlanViewTemplateId is { } templateId && templateId != ElementId.InvalidElementId)
                    view.ViewTemplateId = templateId;
                ApplyScale(view, entry, bbox, output);
                Viewport.Create(doc, sheet.Id, view.Id, cursor);
                cursor = new XYZ(cursor.X + 1.6, cursor.Y, 0);
                viewsCreated++;
            }

            if (viewTypes.AddKeyPlan)
            {
                var keyView = CreateKeyPlan(doc, entry);
                if (keyView is not null)
                {
                    keyView.Name = MakeUnique(existingViewNames, Substitute(output.ViewNameTemplate, entry) + " - Key Plan");
                    Viewport.Create(doc, sheet.Id, keyView.Id, KeyPlanPoint(viewTypes.KeyPlanCorner));
                    viewsCreated++;
                }
                else
                {
                    warnings.Add($"{entry.Number}: couldn't build a key plan (level plan couldn't be duplicated).");
                }
            }

            var rowY = 0.5;

            if (viewTypes.AddCeilingPlan && ceilingPlanVft is not null)
            {
                var view = CreateCroppedPlan(doc, ceilingPlanVft.Id, entry, bbox, output, ViewFamily.CeilingPlan);
                view.Name = MakeUnique(existingViewNames, Substitute(output.ViewNameTemplate, entry) + " - RCP");
                ApplyScale(view, entry, bbox, output);
                Viewport.Create(doc, sheet.Id, view.Id, new XYZ(SheetMarginFeet + 1.0, rowY, 0));
                rowY -= 1.4;
                viewsCreated++;
            }

            if (viewTypes.AddElevations && elevationVft is not null)
            {
                var elevations = CreateElevations(doc, entry, bbox, elevationVft.Id);
                var x = SheetMarginFeet + 0.6;
                foreach (var view in elevations)
                {
                    view.Name = MakeUnique(existingViewNames, Substitute(output.ViewNameTemplate, entry) + $" - Elev {viewsCreated}");
                    Viewport.Create(doc, sheet.Id, view.Id, new XYZ(x, rowY, 0));
                    x += 1.0;
                    viewsCreated++;
                }
                if (elevations.Count > 0) rowY -= 1.2;
            }

            if (viewTypes.AddWallSections && sectionVft is not null)
            {
                var sections = CreateWallSections(doc, entry, sectionVft.Id);
                var x = SheetMarginFeet + 0.6;
                foreach (var view in sections)
                {
                    view.Name = MakeUnique(existingViewNames, Substitute(output.ViewNameTemplate, entry) + $" - Wall Section {viewsCreated}");
                    Viewport.Create(doc, sheet.Id, view.Id, new XYZ(x, rowY, 0));
                    x += 1.0;
                    viewsCreated++;
                }
            }
        }

        return new RoomPlanResult(sheetsCreated, viewsCreated, skipped, warnings);
    }

    private static ViewFamilyType? FirstViewFamilyType(Document doc, ViewFamily family) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(t => t.ViewFamily == family);

    private static ViewPlan CreateCroppedPlan(Document doc, ElementId viewFamilyTypeId, RoomEntry entry, BoundingBoxXYZ bbox, OutputOptions output, ViewFamily family)
    {
        var view = ViewPlan.Create(doc, viewFamilyTypeId, entry.LevelId);
        var crop = view.CropBox;
        var margin = output.CropMarginFeet;
        view.CropBox = new BoundingBoxXYZ
        {
            Transform = crop.Transform,
            Min = new XYZ(bbox.Min.X - margin, bbox.Min.Y - margin, crop.Min.Z),
            Max = new XYZ(bbox.Max.X + margin, bbox.Max.Y + margin, crop.Max.Z),
        };
        view.CropBoxActive = true;
        view.CropBoxVisible = output.ShowCropRegion;

        var shapeManager = view.GetCropRegionShapeManager();
        if (shapeManager.CanHaveAnnotationCrop)
        {
            var offset = output.CropAnnotationsTight ? 0.0 : 2.0;
            shapeManager.TopAnnotationCropOffset = offset;
            shapeManager.BottomAnnotationCropOffset = offset;
            shapeManager.LeftAnnotationCropOffset = offset;
            shapeManager.RightAnnotationCropOffset = offset;
        }

        return view;
    }

    private static void ApplyScale(View view, RoomEntry entry, BoundingBoxXYZ bbox, OutputOptions output)
    {
        var scale = output.ScaleDenominator;
        if (output.AutoFitToSheet)
        {
            var spanFeet = Math.Max(bbox.Max.X - bbox.Min.X, bbox.Max.Y - bbox.Min.Y) + output.CropMarginFeet * 2;
            scale = spanFeet switch
            {
                <= 20 => 25,
                <= 40 => 50,
                <= 80 => 100,
                _ => 200,
            };
        }
        if (View.IsValidViewScale(scale)) view.Scale = scale;
    }

    /// <summary>Fixed sheet-space points for each key-plan corner choice — approximate, not measured against the
    /// title block's real extents (viewports can be dragged into place afterward like any batch-placed sheet).</summary>
    private static XYZ KeyPlanPoint(KeyPlanCorner corner) => corner switch
    {
        KeyPlanCorner.TopLeft => new XYZ(SheetMarginFeet + 0.6, 2.6, 0),
        KeyPlanCorner.BottomRight => new XYZ(SheetMarginFeet + 3.2, 0.6, 0),
        KeyPlanCorner.BottomLeft => new XYZ(SheetMarginFeet + 0.6, 0.6, 0),
        _ => new XYZ(SheetMarginFeet + 3.2, 2.6, 0),
    };

    private static ViewPlan? CreateKeyPlan(Document doc, RoomEntry entry)
    {
        var levelPlan = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewPlan))
            .Cast<ViewPlan>()
            .FirstOrDefault(v => v.GenLevel?.Id == entry.LevelId && !v.IsTemplate && v.ViewType == Autodesk.Revit.DB.ViewType.FloorPlan);

        if (levelPlan is null || !levelPlan.CanViewBeDuplicated(ViewDuplicateOption.AsDependent)) return null;

        var dependentId = levelPlan.Duplicate(ViewDuplicateOption.AsDependent);
        if (doc.GetElement(dependentId) is not ViewPlan keyView) return null;

        var options = new SpatialElementBoundaryOptions();
        var loops = entry.Room.GetBoundarySegments(options)
            .Select(loop => CurveLoop.Create(loop.Select(seg => seg.GetCurve()).ToList()))
            .ToList();

        if (loops.Count > 0)
        {
            var filledRegionType = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>().FirstOrDefault();
            if (filledRegionType is not null)
            {
                try { FilledRegion.Create(doc, filledRegionType.Id, keyView.Id, loops); }
                catch (Exception) { /* boundary geometry not always plane-flat enough for a filled region; key plan still opens without the highlight */ }
            }
        }

        return keyView;
    }

    private static List<ViewSection> CreateElevations(Document doc, RoomEntry entry, BoundingBoxXYZ bbox, ElementId elevationTypeId)
    {
        var levelPlan = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewPlan))
            .Cast<ViewPlan>()
            .FirstOrDefault(v => v.GenLevel?.Id == entry.LevelId && !v.IsTemplate && v.ViewType == Autodesk.Revit.DB.ViewType.FloorPlan);
        if (levelPlan is null) return new List<ViewSection>();

        var center = new XYZ((bbox.Min.X + bbox.Max.X) / 2, (bbox.Min.Y + bbox.Max.Y) / 2, bbox.Min.Z);
        var marker = ElevationMarker.CreateElevationMarker(doc, elevationTypeId, center, 100);

        var views = new List<ViewSection>();
        for (var i = 0; i < 4; i++)
        {
            if (!marker.IsAvailableIndex(i)) continue;
            var view = marker.CreateElevation(doc, levelPlan.Id, i);
            var crop = view.CropBox;
            crop.Min = new XYZ(crop.Min.X, bbox.Min.Z - 0.5, crop.Min.Z);
            crop.Max = new XYZ(crop.Max.X, bbox.Max.Z + 0.5, crop.Max.Z);
            view.CropBox = crop;
            views.Add(view);
        }
        return views;
    }

    private static List<ViewSection> CreateWallSections(Document doc, RoomEntry entry, ElementId sectionTypeId)
    {
        var options = new SpatialElementBoundaryOptions();
        var walls = entry.Room.GetBoundarySegments(options)
            .SelectMany(loop => loop)
            .Select(seg => doc.GetElement(seg.ElementId) as Wall)
            .Where(w => w is not null)
            .Cast<Wall>()
            .GroupBy(w => w.Id)
            .Select(g => g.First())
            .OrderByDescending(w => (w.Location as LocationCurve)?.Curve.Length ?? 0)
            .Take(6) // caps section count per room; walls ranked longest-first as a stand-in for "importance"
            .ToList();

        var views = new List<ViewSection>();
        foreach (var wall in walls)
        {
            if (wall.Location is not LocationCurve locationCurve || locationCurve.Curve is not Line line) continue;

            var direction = line.Direction.Normalize();
            var normal = wall.Orientation.Normalize();
            var wallBbox = wall.get_BoundingBox(null);
            if (wallBbox is null) continue;

            var halfLength = line.Length / 2 + 1.0;
            var halfHeight = (wallBbox.Max.Z - wallBbox.Min.Z) / 2 + 0.5;
            var midpoint = line.Evaluate(0.5, true);

            var transform = Transform.Identity;
            transform.Origin = midpoint;
            transform.BasisX = direction;
            transform.BasisY = XYZ.BasisZ;
            transform.BasisZ = normal;

            var sectionBox = new BoundingBoxXYZ
            {
                Transform = transform,
                Min = new XYZ(-halfLength, -halfHeight, -wall.Width),
                Max = new XYZ(halfLength, halfHeight, wall.Width * 3),
            };

            try { views.Add(ViewSection.CreateSection(doc, sectionTypeId, sectionBox)); }
            catch (Exception) { /* a handful of odd wall geometries (curved, sloped) can't host a straight section box; skip those */ }
        }
        return views;
    }

    private static void DeletePreviouslyGenerated(Document doc, List<RoomEntry> rooms, OutputOptions output)
    {
        var wantedSheetNames = rooms.Select(r => RoomPlanGenerator.Substitute(output.SheetNameTemplate, r)).ToHashSet();
        var toDelete = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .Where(s => wantedSheetNames.Contains(s.Name))
            .Select(s => s.Id)
            .ToList();
        if (toDelete.Count > 0) doc.Delete(toDelete);
    }

    private static void ApplyBrowserSort(ViewSheet sheet, RoomEntry entry, OutputOptions output)
    {
        var value = output.BrowserSortMode switch
        {
            "Level" => entry.LevelName,
            "Department" => entry.Department,
            "Custom" => output.SortValue,
            _ => null,
        };
        if (value is not null) TrySetParameter(sheet, "Sort Value", value);
    }

    private static void TrySetParameter(Element element, string name, string value)
    {
        var p = element.LookupParameter(name);
        if (p is not null && !p.IsReadOnly && p.StorageType == StorageType.String) p.Set(value);
    }

    private static HashSet<string> ExistingViewOrSheetNames(Document doc, bool isSheet)
    {
        if (isSheet)
            return new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.Name).ToHashSet();
        return new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate).Select(v => v.Name).ToHashSet();
    }

    private static HashSet<string> ExistingSheetNumbers(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.SheetNumber).ToHashSet();

    private static string MakeUnique(HashSet<string> existing, string baseValue)
    {
        if (!existing.Contains(baseValue)) { existing.Add(baseValue); return baseValue; }
        var i = 2;
        while (existing.Contains($"{baseValue} {i}")) i++;
        var result = $"{baseValue} {i}";
        existing.Add(result);
        return result;
    }

    /// <summary>
    /// Bumps the trailing digit run of a starting sheet number ("A-301" -> "A-302", "A-303", ...),
    /// falling back to a plain " 2", " 3" suffix if the seed has no trailing digits to increment.
    /// </summary>
    private static string IncrementSheetNumber(string seed, int offset)
    {
        if (offset == 0) return seed;

        var i = seed.Length;
        while (i > 0 && char.IsDigit(seed[i - 1])) i--;
        var prefix = seed[..i];
        var digits = seed[i..];

        if (digits.Length == 0) return $"{seed} {offset + 1}";

        var next = int.Parse(digits) + offset;
        return prefix + next.ToString().PadLeft(digits.Length, '0');
    }
}
