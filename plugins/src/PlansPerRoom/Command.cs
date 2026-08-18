using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.PlansPerRoom;

/// <summary>
/// Creates one new floor plan view per placed room, cropped to that room's
/// bounding box plus a margin and renamed from the room number/name.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "plansperroom";

    private const double MarginFeet = 3.0;

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var rooms = new FilteredElementCollector(doc)
            .OfClass(typeof(SpatialElement))
            .OfType<Room>()
            .Where(r => r.Area > 0)
            .ToList();

        if (rooms.Count == 0)
        {
            TaskDialog.Show("BIMFlow — PlansPerRoom", "No placed rooms were found in this project.");
            return Result.Succeeded;
        }

        var floorPlanType = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(t => t.ViewFamily == ViewFamily.FloorPlan);

        if (floorPlanType is null)
        {
            TaskDialog.Show("BIMFlow — PlansPerRoom", "No floor plan view type is available in this project.");
            return Result.Cancelled;
        }

        var created = 0;
        var skipped = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Create Plans Per Room");
        transaction.Start();
        try
        {
            foreach (var room in rooms)
            {
                var bbox = room.get_BoundingBox(null);
                if (bbox is null || room.LevelId == ElementId.InvalidElementId)
                {
                    skipped++;
                    continue;
                }

                var view = ViewPlan.Create(doc, floorPlanType.Id, room.LevelId);
                var originalCrop = view.CropBox;

                view.CropBox = new BoundingBoxXYZ
                {
                    Transform = originalCrop.Transform,
                    Min = new XYZ(bbox.Min.X - MarginFeet, bbox.Min.Y - MarginFeet, originalCrop.Min.Z),
                    Max = new XYZ(bbox.Max.X + MarginFeet, bbox.Max.Y + MarginFeet, originalCrop.Max.Z),
                };
                view.CropBoxActive = true;
                view.CropBoxVisible = true;

                var roomLabel = string.IsNullOrWhiteSpace(room.Name) ? room.Number : $"{room.Number} - {room.Name}";
                view.Name = MakeUniqueName(doc, $"Room Plan - {roomLabel}");

                created++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "BIMFlow — PlansPerRoom",
            $"Created {created} room plan view(s)." + (skipped > 0 ? $" Skipped {skipped} room(s) without usable geometry." : ""));

        return Result.Succeeded;
    }

    private static string MakeUniqueName(Document doc, string baseName)
    {
        var existingNames = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Select(v => v.Name)
            .ToHashSet();

        if (!existingNames.Contains(baseName)) return baseName;

        var i = 2;
        while (existingNames.Contains($"{baseName} {i}")) i++;
        return $"{baseName} {i}";
    }
}
