using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.ModelCeilings;

/// <summary>
/// Creates a ceiling for every room in the active floor plan directly from
/// its boundary. Skips rooms whose bounding box already overlaps an
/// existing ceiling (a fast heuristic for "already has one"), and skips
/// any room whose boundary can't be resolved into closed loops.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "modelceilings";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        if (view is not ViewPlan)
        {
            TaskDialog.Show("BIMFlow — ModelCeilings", "Switch to a floor plan view first.");
            return Result.Cancelled;
        }

        var rooms = new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(SpatialElement))
            .OfType<Room>()
            .Where(r => r.Area > 0)
            .ToList();

        if (rooms.Count == 0)
        {
            TaskDialog.Show("BIMFlow — ModelCeilings", "No placed rooms are visible in the active view.");
            return Result.Succeeded;
        }

        var ceilingTypes = new FilteredElementCollector(doc)
            .OfClass(typeof(CeilingType))
            .Cast<CeilingType>()
            .OrderBy(t => t.Name)
            .ToList();

        if (ceilingTypes.Count == 0)
        {
            TaskDialog.Show("BIMFlow — ModelCeilings", "No ceiling types are loaded in this project.");
            return Result.Succeeded;
        }

        var window = new ModelCeilingsWindow(ceilingTypes, rooms.Count);
        if (window.ShowDialog() != true || window.SelectedType is null)
            return Result.Cancelled;

        var existingCeilingBoxes = new FilteredElementCollector(doc)
            .OfClass(typeof(Ceiling))
            .Select(c => c.get_BoundingBox(null))
            .Where(b => b is not null)
            .ToList();

        var created = 0;
        var skipped = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Create Ceilings from Rooms");
        transaction.Start();
        try
        {
            var options = new SpatialElementBoundaryOptions();

            foreach (var room in rooms)
            {
                var roomBox = room.get_BoundingBox(null);
                if (roomBox is not null && existingCeilingBoxes.Any(b => BoundingBoxesOverlap(b!, roomBox)))
                {
                    skipped++;
                    continue;
                }

                var loops = BuildCurveLoops(room, options);
                if (loops.Count == 0 || room.LevelId == ElementId.InvalidElementId)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var ceiling = Ceiling.Create(doc, loops, window.SelectedType.Id, room.LevelId);
                    var heightParam = ceiling.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM);
                    heightParam?.Set(window.HeightOffsetFeet);
                    created++;
                }
                catch (Exception)
                {
                    skipped++;
                }
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — ModelCeilings", $"Created {created} ceiling(s). Skipped {skipped}.");
        return Result.Succeeded;
    }

    private static List<CurveLoop> BuildCurveLoops(Room room, SpatialElementBoundaryOptions options)
    {
        var loops = new List<CurveLoop>();
        var segmentLoops = room.GetBoundarySegments(options);
        if (segmentLoops is null) return loops;

        foreach (var segments in segmentLoops)
        {
            try
            {
                var curveLoop = new CurveLoop();
                foreach (var segment in segments)
                    curveLoop.Append(segment.GetCurve());
                loops.Add(curveLoop);
            }
            catch (Exception)
            {
                // Malformed boundary loop (gaps, self-intersections) — skip just this loop.
            }
        }

        return loops;
    }

    private static bool BoundingBoxesOverlap(BoundingBoxXYZ a, BoundingBoxXYZ b)
    {
        return a.Min.X <= b.Max.X && b.Min.X <= a.Max.X
            && a.Min.Y <= b.Max.Y && b.Min.Y <= a.Max.Y
            && a.Min.Z <= b.Max.Z && b.Min.Z <= a.Max.Z;
    }
}
