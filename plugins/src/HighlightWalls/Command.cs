using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.HighlightWalls;

/// <summary>
/// One click colors every wall in the active view by its Function
/// parameter (interior/exterior/etc.), a second click clears it back to
/// default. Detects the current state by checking whether the first wall's
/// override already matches one of this tool's colors, rather than storing
/// separate state that could get out of sync with manual V/G edits.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "highlightwalls";

    private static readonly Color InteriorColor = new(0, 130, 200);
    private static readonly Color ExteriorColor = new(230, 25, 75);
    private static readonly Color OtherColor = new(150, 150, 150);

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var walls = new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(Wall))
            .Cast<Wall>()
            .ToList();

        if (walls.Count == 0)
        {
            TaskDialog.Show("BIMFlow — HighlightWalls", "No walls are visible in the active view.");
            return Result.Succeeded;
        }

        var currentlyOn = walls
            .Select(w => view.GetElementOverrides(w.Id).ProjectionLineColor)
            .Where(c => c.IsValid)
            .Any(c => ColorsEqual(c, InteriorColor) || ColorsEqual(c, ExteriorColor));

        using var transaction = new Transaction(doc, currentlyOn ? "BIMFlow: Clear Wall Highlight" : "BIMFlow: Highlight Walls");
        transaction.Start();
        try
        {
            foreach (var wall in walls)
            {
                if (currentlyOn)
                {
                    view.SetElementOverrides(wall.Id, new OverrideGraphicSettings());
                    continue;
                }

                var function = (wall.WallType?.Function) ?? WallFunction.Interior;
                var color = function switch
                {
                    WallFunction.Interior => InteriorColor,
                    WallFunction.Exterior => ExteriorColor,
                    _ => OtherColor,
                };

                var overrides = new OverrideGraphicSettings().SetProjectionLineColor(color).SetCutLineColor(color);
                view.SetElementOverrides(wall.Id, overrides);
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "BIMFlow — HighlightWalls",
            currentlyOn
                ? $"Cleared the highlight on {walls.Count} wall(s)."
                : $"Highlighted {walls.Count} wall(s) — blue for interior, red for exterior, gray for other.");

        return Result.Succeeded;
    }

    private static bool ColorsEqual(Color a, Color b) => a.Red == b.Red && a.Green == b.Green && a.Blue == b.Blue;
}
