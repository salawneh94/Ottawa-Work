using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace OttawaWork.Shared;

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
    bool AddCeilingPlan,
    ElementId? CeilingPlanViewTemplateId = null);

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
        using var transaction = new Transaction(doc, "Ottawa Tools: Save Room Finish Parameters");
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
        var levelExtentCache = new Dictionary<ElementId, BoundingBoxXYZ?>();

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

            // The title block instance Revit just placed via ViewSheet.Create
            // needs a regenerate before its geometry (and therefore its
            // bounding box) is actually computed — confirmed live (user-
            // reported): without this, GetSheetContentArea below could read
            // a stale/incomplete bounding box for a just-created title block,
            // making the packer think it had a much smaller sheet to work
            // with than the real one, cramming every viewport into one small
            // corner while the rest of the (actually much bigger) sheet sat
            // empty.
            doc.Regenerate();

            var content = GetSheetContentArea(doc, sheet);
            var packer = new SheetLayoutPacker(content.MinX, content.MaxX, content.MinY, content.MaxY);

            if (viewTypes.CreateFloorPlan && floorPlanVft is not null)
            {
                var view = CreateCroppedPlan(doc, floorPlanVft.Id, entry, bbox, output, ViewFamily.FloorPlan, viewTypes.FloorPlanViewTemplateId);
                view.Name = MakeUnique(existingViewNames, Substitute(output.ViewNameTemplate, entry));
                ApplyScale(view, entry, bbox, output);
                packer.Place(Viewport.Create(doc, sheet.Id, view.Id, content.TopLeft));
                viewsCreated++;
            }

            if (viewTypes.AddKeyPlan)
            {
                var keyView = CreateKeyPlan(doc, entry);
                if (keyView is not null)
                {
                    keyView.Name = MakeUnique(existingViewNames, Substitute(output.ViewNameTemplate, entry) + " - Key Plan");
                    ApplyKeyPlanScale(doc, keyView, entry.LevelId, levelExtentCache);
                    var keyViewport = Viewport.Create(doc, sheet.Id, keyView.Id, content.TopLeft);
                    keyViewport.SetBoxCenter(KeyPlanPoint(viewTypes.KeyPlanCorner, keyViewport, content));
                    viewsCreated++;
                }
                else
                {
                    warnings.Add($"{entry.Number}: couldn't build a key plan (level plan couldn't be duplicated).");
                }
            }

            if (viewTypes.AddCeilingPlan && ceilingPlanVft is not null)
            {
                var view = CreateCroppedPlan(doc, ceilingPlanVft.Id, entry, bbox, output, ViewFamily.CeilingPlan, viewTypes.CeilingPlanViewTemplateId);
                view.Name = MakeUnique(existingViewNames, Substitute(output.ViewNameTemplate, entry) + " - RCP");
                ApplyScale(view, entry, bbox, output);
                packer.Place(Viewport.Create(doc, sheet.Id, view.Id, content.TopLeft));
                viewsCreated++;
            }

            if (viewTypes.AddElevations && elevationVft is not null)
            {
                // Scale is applied inside CreateElevations itself, from the
                // elevation's own real crop width/height — not the room's
                // plan footprint (see ApplySpanScale there): confirmed live
                // (user-reported) that elevations came out at a visibly
                // different/wrong scale from the floor plan when this used
                // to reuse the plan-footprint-based ApplyScale instead.
                var elevations = CreateElevations(doc, entry, bbox, elevationVft.Id, output);
                foreach (var view in elevations)
                {
                    view.Name = MakeUnique(existingViewNames, Substitute(output.ViewNameTemplate, entry) + $" - Elev {viewsCreated}");
                    packer.Place(Viewport.Create(doc, sheet.Id, view.Id, content.TopLeft));
                    viewsCreated++;
                }
            }

            if (viewTypes.AddWallSections && sectionVft is not null)
            {
                var sections = CreateWallSections(doc, entry, sectionVft.Id);
                foreach (var view in sections)
                {
                    view.Name = MakeUnique(existingViewNames, Substitute(output.ViewNameTemplate, entry) + $" - Wall Section {viewsCreated}");
                    ApplyScale(view, entry, bbox, output);
                    packer.Place(Viewport.Create(doc, sheet.Id, view.Id, content.TopLeft));
                    viewsCreated++;
                }
            }

            if (packer.Overflowed)
                warnings.Add($"{entry.Number}: views didn't all fit within the sheet's usable area — some may overlap the title block or run past its edge. Try a larger title block, fewer view types, or a tighter crop margin.");
        }

        return new RoomPlanResult(sheetsCreated, viewsCreated, skipped, warnings);
    }

    private readonly record struct SheetContentArea(double MinX, double MaxX, double MinY, double MaxY)
    {
        public XYZ TopLeft => new(MinX, MaxY, 0);
    }

    /// <summary>Reads the sheet's real usable area from the title block instance Revit placed when the
    /// sheet was created — confirmed live (user-reported): the old fixed sheet-space offsets took no
    /// account of the title block's actual paper size (or the room's actual size), so views routinely
    /// ran off small sheets and left large sheets mostly empty. Falls back to a generic size only if a
    /// title block instance somehow couldn't be found, which shouldn't normally happen.</summary>
    private static SheetContentArea GetSheetContentArea(Document doc, ViewSheet sheet)
    {
        var titleBlock = new FilteredElementCollector(doc, sheet.Id)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsNotElementType()
            .FirstOrDefault();
        var bbox = titleBlock?.get_BoundingBox(sheet);
        var min = bbox?.Min ?? new XYZ(0, 0, 0);
        var max = bbox?.Max ?? new XYZ(3.0, 2.0, 0);

        return new SheetContentArea(min.X + SheetMarginFeet, max.X - SheetMarginFeet, min.Y + SheetMarginFeet, max.Y - SheetMarginFeet);
    }

    /// <summary>
    /// Places each viewport by actually measuring its real on-sheet footprint after creation
    /// (Viewport.GetBoxOutline — the crop plus Revit's own view-title label, exactly as it will print)
    /// and flowing left-to-right, top-to-bottom within the sheet's real usable area, wrapping to a new
    /// row whenever the next viewport wouldn't fit on the current one — instead of guessing a size up
    /// front the way a fixed-offset layout has to.
    /// </summary>
    private sealed class SheetLayoutPacker
    {
        private const double Gap = 0.15;
        private readonly double _minX, _maxX, _minY;
        private double _cursorX, _cursorY, _rowTallest;

        public bool Overflowed { get; private set; }

        public SheetLayoutPacker(double minX, double maxX, double minY, double maxY)
        {
            _minX = minX;
            _maxX = maxX;
            _minY = minY;
            _cursorX = minX;
            _cursorY = maxY;
        }

        public void Place(Viewport viewport)
        {
            var outline = viewport.GetBoxOutline();
            var width = outline.MaximumPoint.X - outline.MinimumPoint.X;
            var height = outline.MaximumPoint.Y - outline.MinimumPoint.Y;

            if (_cursorX > _minX && _cursorX + width > _maxX)
            {
                _cursorX = _minX;
                _cursorY -= _rowTallest + Gap;
                _rowTallest = 0;
            }

            viewport.SetBoxCenter(new XYZ(_cursorX + width / 2, _cursorY - height / 2, 0));
            if (_cursorY - height < _minY) Overflowed = true;

            _cursorX += width + Gap;
            _rowTallest = Math.Max(_rowTallest, height);
        }
    }

    private static ViewFamilyType? FirstViewFamilyType(Document doc, ViewFamily family) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(t => t.ViewFamily == family);

    private static ViewPlan CreateCroppedPlan(Document doc, ElementId viewFamilyTypeId, RoomEntry entry, BoundingBoxXYZ bbox, OutputOptions output, ViewFamily family, ElementId? viewTemplateId = null)
    {
        var view = ViewPlan.Create(doc, viewFamilyTypeId, entry.LevelId);

        // The view template has to go on BEFORE the crop is set, not after —
        // confirmed live (user-reported): a template that controls Crop
        // View/Crop Region Visible/View Range as one of its own governed
        // parameters silently overwrote the per-room crop the instant it was
        // applied, leaving the "cropped" plan showing the whole level. Since
        // View.ViewTemplateId assignment re-applies every parameter the
        // template governs right then, whichever of CropBox/ViewTemplateId
        // is set SECOND is the one that wins — this always sets the template
        // first so our explicit per-room crop, set below, is what sticks.
        if (viewTemplateId is { } templateId && templateId != ElementId.InvalidElementId)
            view.ViewTemplateId = templateId;

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
        var spanFeet = Math.Max(bbox.Max.X - bbox.Min.X, bbox.Max.Y - bbox.Min.Y) + output.CropMarginFeet * 2;
        ApplySpanScale(view, spanFeet, output);
    }

    private static void ApplySpanScale(View view, double spanFeet, OutputOptions output)
    {
        var scale = output.ScaleDenominator;
        if (output.AutoFitToSheet)
        {
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

    /// <summary>Centers the key-plan viewport in whichever corner of the sheet's real usable area was
    /// chosen, using the viewport's own actually-measured footprint (GetBoxOutline — same technique the
    /// main layout packer uses) so it sits fully inside that area regardless of the title block's real
    /// size or the key plan's own scale — replacing fixed sheet-space offsets that took no account of
    /// either (confirmed live, user-reported, alongside the rest of this batch's "views don't fit").</summary>
    private static XYZ KeyPlanPoint(KeyPlanCorner corner, Viewport keyViewport, SheetContentArea content)
    {
        var outline = keyViewport.GetBoxOutline();
        var halfWidth = (outline.MaximumPoint.X - outline.MinimumPoint.X) / 2;
        var halfHeight = (outline.MaximumPoint.Y - outline.MinimumPoint.Y) / 2;

        return corner switch
        {
            KeyPlanCorner.TopLeft => new XYZ(content.MinX + halfWidth, content.MaxY - halfHeight, 0),
            KeyPlanCorner.BottomRight => new XYZ(content.MaxX - halfWidth, content.MinY + halfHeight, 0),
            KeyPlanCorner.BottomLeft => new XYZ(content.MinX + halfWidth, content.MinY + halfHeight, 0),
            _ => new XYZ(content.MaxX - halfWidth, content.MaxY - halfHeight, 0), // TopRight
        };
    }

    private static void ApplyKeyPlanScale(Document doc, ViewPlan keyView, ElementId levelId, Dictionary<ElementId, BoundingBoxXYZ?> levelExtentCache)
    {
        // A key plan is a small locator thumbnail, not a full readable
        // drawing — confirmed live (user-reported): duplicating the parent
        // level view left it at whatever scale that level plan already
        // happened to be set to, which for the WHOLE level crammed into a
        // sheet corner is usually far too large. Picks whatever standard
        // scale keeps the level's rough extent to about a foot and a half
        // on the actual sheet instead. Cached per level (not per room) so a
        // multi-room batch on the same level doesn't re-collect the whole
        // level's geometry once per room.
        if (!levelExtentCache.TryGetValue(levelId, out var extent))
        {
            extent = LevelExtent(doc, levelId);
            levelExtentCache[levelId] = extent;
        }
        if (extent is null) return;

        var spanFeet = Math.Max(extent.Max.X - extent.Min.X, extent.Max.Y - extent.Min.Y);
        const double targetSheetFeet = 1.4;
        var scale = Math.Max(50, (int)Math.Ceiling(spanFeet / targetSheetFeet / 10.0) * 10);
        if (View.IsValidViewScale(scale)) keyView.Scale = scale;
    }

    private static BoundingBoxXYZ? LevelExtent(Document doc, ElementId levelId)
    {
        // Room can't be passed to ElementClassFilter/OfClass directly —
        // confirmed live (user-reported): Revit throws "Input type(...
        // Room) is of an element type that exists in the API, but not in
        // Revit's native object model" for that, same as CollectRooms above
        // already has to work around by filtering SpatialElement and then
        // narrowing to Room in a second pass instead.
        var walls = new FilteredElementCollector(doc)
            .OfClass(typeof(Wall))
            .WhereElementIsNotElementType()
            .Where(e => e.LevelId == levelId)
            .Cast<Element>();
        var rooms = new FilteredElementCollector(doc)
            .OfClass(typeof(SpatialElement))
            .OfType<Room>()
            .Where(e => e.LevelId == levelId)
            .Cast<Element>();
        var elements = walls.Concat(rooms).ToList();

        BoundingBoxXYZ? union = null;
        foreach (var element in elements)
        {
            var bb = element.get_BoundingBox(null);
            if (bb is null) continue;
            union = union is null
                ? bb
                : new BoundingBoxXYZ
                {
                    Min = new XYZ(Math.Min(union.Min.X, bb.Min.X), Math.Min(union.Min.Y, bb.Min.Y), Math.Min(union.Min.Z, bb.Min.Z)),
                    Max = new XYZ(Math.Max(union.Max.X, bb.Max.X), Math.Max(union.Max.Y, bb.Max.Y), Math.Max(union.Max.Z, bb.Max.Z)),
                };
        }
        return union;
    }

    private static ViewPlan? CreateKeyPlan(Document doc, RoomEntry entry)
    {
        var levelPlan = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewPlan))
            .Cast<ViewPlan>()
            .FirstOrDefault(v => v.GenLevel?.Id == entry.LevelId && !v.IsTemplate && v.ViewType == Autodesk.Revit.DB.ViewType.FloorPlan);

        if (levelPlan is null || !levelPlan.CanViewBeDuplicated(ViewDuplicateOption.AsDependent)) return null;

        var dependentId = levelPlan.Duplicate(ViewDuplicateOption.AsDependent);
        if (doc.GetElement(dependentId) is not ViewPlan keyView) return null;

        HighlightRoomOnKeyPlan(doc, keyView, entry.Room);
        HideMarkerClutterOnKeyPlan(keyView);

        return keyView;
    }

    /// <summary>Highlights the room via a view-specific element override (color + partial transparency)
    /// instead of a FilledRegion — confirmed live (user-reported): the old FilledRegion approach used
    /// WHATEVER FilledRegionType happened to be first in the project (FirstOrDefault(), no control over
    /// which one), and on this project that was a fully opaque black fill that hid the room's own
    /// content underneath entirely, rather than just marking which room is "this one" on the level-wide
    /// key plan. SetElementOverrides doesn't depend on which fill region types a given project happens
    /// to have, and 70% transparency keeps whatever's under the highlight still visible.</summary>
    private static void HighlightRoomOnKeyPlan(Document doc, ViewPlan keyView, Room room)
    {
        var color = new Color(255, 105, 0);
        var overrides = new OverrideGraphicSettings().SetProjectionLineColor(color).SetProjectionLineWeight(6);

        var solidFillPatternId = new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(f => f.GetFillPattern() is { IsSolidFill: true, Target: FillPatternTarget.Drafting })
            ?.Id;
        if (solidFillPatternId is not null)
        {
            overrides = overrides
                .SetSurfaceForegroundPatternId(solidFillPatternId)
                .SetSurfaceForegroundPatternColor(color)
                .SetSurfaceForegroundPatternVisible(true)
                .SetSurfaceTransparency(70);
        }

        keyView.SetElementOverrides(room.Id, overrides);
    }

    /// <summary>Hides elevation-marker and section-head categories on the key plan — confirmed live
    /// (user-reported): every room's elevation markers/section heads are hosted on the SAME shared level
    /// plan (CreateElevations/CreateWallSections below both look that view up fresh each call, same
    /// query as here), and a dependent view shows everything visible on its parent, so a multi-room batch
    /// left every room's key plan cluttered with every OTHER room's elevation/section markers too, not
    /// just its own. A key plan's whole job is being a quick, uncluttered "you are here" locator diagram —
    /// nobody needs to see elevation tags on it at all, this room's own included.</summary>
    private static void HideMarkerClutterOnKeyPlan(ViewPlan keyView)
    {
        foreach (var category in new[] { BuiltInCategory.OST_Elev, BuiltInCategory.OST_Sections })
        {
            var categoryId = new ElementId(category);
            if (keyView.CanCategoryBeHidden(categoryId))
                keyView.SetCategoryHidden(categoryId, true);
        }
    }

    private static List<ViewSection> CreateElevations(Document doc, RoomEntry entry, BoundingBoxXYZ bbox, ElementId elevationTypeId, OutputOptions output)
    {
        var levelPlan = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewPlan))
            .Cast<ViewPlan>()
            .FirstOrDefault(v => v.GenLevel?.Id == entry.LevelId && !v.IsTemplate && v.ViewType == Autodesk.Revit.DB.ViewType.FloorPlan);
        if (levelPlan is null) return new List<ViewSection>();

        var center = new XYZ((bbox.Min.X + bbox.Max.X) / 2, (bbox.Min.Y + bbox.Max.Y) / 2, bbox.Min.Z);
        var marker = ElevationMarker.CreateElevationMarker(doc, elevationTypeId, center, 100);

        // Half-width covering the room from any of the marker's 4 facing
        // directions — using the larger of the room's two footprint
        // dimensions is a conservative, safe choice (never clips the room
        // regardless of which way a given elevation actually faces) rather
        // than computing an exact per-direction width.
        var halfWidth = Math.Max(bbox.Max.X - bbox.Min.X, bbox.Max.Y - bbox.Min.Y) / 2 + 1.0;

        var views = new List<ViewSection>();
        for (var i = 0; i < 4; i++)
        {
            if (!marker.IsAvailableIndex(i)) continue;
            var view = marker.CreateElevation(doc, levelPlan.Id, i);
            var crop = view.CropBox;

            // Confirmed live (user-reported): only the vertical (Y, height)
            // extent was ever narrowed here — the horizontal (X, width)
            // extent was left at whatever generic default
            // ElevationMarker.CreateElevation produces, which is far wider
            // than a typical room, so every elevation came out "big, not
            // just to this room." Narrowed symmetrically around the
            // default's own existing center (not assumed to be X=0) so this
            // doesn't depend on exactly where CreateElevation happens to
            // center its default crop.
            var centerX = (crop.Min.X + crop.Max.X) / 2;
            crop.Min = new XYZ(centerX - halfWidth, bbox.Min.Z - 0.5, crop.Min.Z);
            crop.Max = new XYZ(centerX + halfWidth, bbox.Max.Z + 0.5, crop.Max.Z);
            view.CropBox = crop;
            view.CropBoxActive = true;

            // Confirmed live (user-reported): reusing floor-plan ApplyScale
            // here picked a scale from the room's PLAN footprint — wrong
            // basis for an elevation, whose own content is this crop's real
            // width x height, not the room's plan dimensions (a small room
            // with a tall ceiling, for instance, needs a scale that fits the
            // height too, which the plan footprint alone says nothing about).
            var heightFeet = bbox.Max.Z - bbox.Min.Z + 1.0;
            ApplySpanScale(view, Math.Max(halfWidth * 2, heightFeet), output);

            views.Add(view);
        }
        return views;
    }

    private static List<ViewSection> CreateWallSections(Document doc, RoomEntry entry, ElementId sectionTypeId)
    {
        // Built from the boundary SEGMENT's own curve, not the whole Wall's
        // LocationCurve — confirmed live (user-reported): a wall that runs
        // well beyond the room (e.g. the length of a corridor, only
        // bordering this one room for a few feet of that) was producing a
        // section sized — and CENTERED — on the wall's entire modeled
        // length, not the room, which is exactly "big, not just to this
        // room" (and could even center the section somewhere else on the
        // wall entirely, nowhere near this room, for a long wall). A room
        // boundary can revisit the same wall more than once (an L-shaped
        // wrap around a corner, for instance), so duplicates against one
        // wall are resolved by keeping its longest bordering segment.
        var options = new SpatialElementBoundaryOptions();
        var segments = entry.Room.GetBoundarySegments(options)
            .SelectMany(loop => loop)
            .Select(seg => (Wall: doc.GetElement(seg.ElementId) as Wall, Curve: seg.GetCurve()))
            .Where(t => t.Wall is not null)
            .GroupBy(t => t.Wall!.Id)
            .Select(g => g.OrderByDescending(t => t.Curve.Length).First())
            .OrderByDescending(t => t.Curve.Length)
            .Take(6) // caps section count per room; segments ranked longest-first as a stand-in for "importance"
            .ToList();

        var views = new List<ViewSection>();
        foreach (var (wall, curve) in segments)
        {
            if (curve is not Line line) continue;

            var direction = line.Direction.Normalize();
            var normal = wall!.Orientation.Normalize();
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
