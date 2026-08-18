using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.WallJoinFixer;

/// <summary>
/// Re-enables wall-end joins across a set of walls and joins the geometry of
/// any walls whose bounding boxes overlap but haven't been joined — the two
/// most common causes of stray unjoined-wall lines in a plan view.
/// Scope: current selection if any walls are selected, otherwise every wall
/// visible in the active view.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "walljoinfixer";
    private const double ToleranceFeet = 0.05; // ~0.6" — close enough to be "touching"

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var selectedWalls = uiDoc.Selection.GetElementIds()
            .Select(id => doc.GetElement(id))
            .OfType<Wall>()
            .ToList();

        var walls = selectedWalls.Count > 0
            ? selectedWalls
            : new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();

        if (walls.Count < 1)
        {
            TaskDialog.Show("BIMFlow — Wall Join Fixer", "No walls found in the current selection or active view.");
            return Result.Succeeded;
        }

        var endsFixed = 0;
        var pairsJoined = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Fix Wall Joins");
        transaction.Start();

        try
        {
            foreach (var wall in walls)
            {
                foreach (var end in new[] { 0, 1 })
                {
                    if (WallUtils.IsWallJoinAllowedAtEnd(wall, end)) continue;
                    WallUtils.AllowWallJoinAtEnd(wall, end);
                    endsFixed++;
                }
            }

            for (var i = 0; i < walls.Count; i++)
            {
                for (var j = i + 1; j < walls.Count; j++)
                {
                    var a = walls[i];
                    var b = walls[j];

                    if (JoinGeometryUtils.AreElementsJoined(doc, a, b)) continue;
                    if (!BoundingBoxesNearlyIntersect(a, b, doc.ActiveView)) continue;

                    try
                    {
                        JoinGeometryUtils.JoinGeometry(doc, a, b);
                        pairsJoined++;
                    }
                    catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                    {
                        // Geometry that can't be joined (e.g. parallel non-touching walls) — skip it.
                    }
                }
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "BIMFlow — Wall Join Fixer",
            $"Re-enabled {endsFixed} wall-end join(s) and joined {pairsJoined} wall pair(s).");

        return Result.Succeeded;
    }

    private static bool BoundingBoxesNearlyIntersect(Element a, Element b, View view)
    {
        var boxA = a.get_BoundingBox(view);
        var boxB = b.get_BoundingBox(view);
        if (boxA is null || boxB is null) return false;

        return boxA.Min.X - ToleranceFeet <= boxB.Max.X && boxB.Min.X - ToleranceFeet <= boxA.Max.X
            && boxA.Min.Y - ToleranceFeet <= boxB.Max.Y && boxB.Min.Y - ToleranceFeet <= boxA.Max.Y
            && boxA.Min.Z - ToleranceFeet <= boxB.Max.Z && boxB.Min.Z - ToleranceFeet <= boxA.Max.Z;
    }
}
