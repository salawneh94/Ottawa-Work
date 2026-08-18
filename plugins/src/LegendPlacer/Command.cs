using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.LegendPlacer;

/// <summary>
/// Batch-places one legend view onto many sheets at the same position.
/// Legend views are the one Revit view type that can be placed on more
/// than one sheet at a time, which is exactly what makes batch placement
/// worth automating.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "legendplacer";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var legends = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.ViewType == ViewType.Legend)
            .OrderBy(v => v.Name)
            .ToList();

        if (legends.Count == 0)
        {
            TaskDialog.Show("BIMFlow — LegendPlacer", "No legend views were found in this project.");
            return Result.Succeeded;
        }

        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .OrderBy(s => s.SheetNumber)
            .ToList();

        var window = new LegendPlacerWindow(legends, sheets);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedLegend is null || window.SelectedSheets.Count == 0)
            return Result.Succeeded;

        var placed = 0;
        var skipped = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Place Legend on Sheets");
        transaction.Start();
        try
        {
            foreach (var sheet in window.SelectedSheets)
            {
                if (!Viewport.CanAddViewToSheet(doc, sheet.Id, window.SelectedLegend.Id))
                {
                    skipped++;
                    continue;
                }

                Viewport.Create(doc, sheet.Id, window.SelectedLegend.Id, window.Position);
                placed++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "BIMFlow — LegendPlacer",
            $"Placed the legend on {placed} sheet(s).{(skipped > 0 ? $" Skipped {skipped} (already placed there)." : "")}");

        return Result.Succeeded;
    }
}
