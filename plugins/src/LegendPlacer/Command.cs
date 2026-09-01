using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.LegendPlacer;

/// <summary>
/// Batch-places one legend view onto many sheets — either by copying the
/// exact position from a sheet that already has it placed, or by anchoring
/// to a title block corner with an inset offset (LegendPlacerEngine).
/// Legend views are the one Revit view type that can be placed on more
/// than one sheet at a time, which is exactly what makes batch placement
/// worth automating.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
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
            TaskDialog.Show("Ottawa Tools — LegendPlacer", "No legend views were found in this project.");
            return Result.Succeeded;
        }

        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .OrderBy(s => s.SheetNumber)
            .ToList();

        var window = new LegendPlacerWindow(doc, legends, sheets);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedLegend is null || window.SelectedSheets.Count == 0)
            return Result.Succeeded;

        var placed = 0;
        var skipped = 0;

        // Copy-from-reference-sheet resolves to one position shared by every
        // target sheet (that's the whole point — matching an existing
        // placement exactly); anchor-to-corner is resolved per sheet inside
        // the loop instead, since each sheet's title block can sit at a
        // different spot (or be a different size) even within one project.
        XYZ? sharedPosition = null;
        if (window.PositionMethod == LegendPositionMethod.CopyFromReferenceSheet && window.ReferenceSheetId is { } refSheetId)
        {
            var placements = LegendPlacerEngine.ExistingPlacements(doc, window.SelectedLegend.Id);
            if (placements.TryGetValue(refSheetId, out var refViewport))
                sharedPosition = refViewport.GetBoxCenter();
        }

        using var transaction = new Transaction(doc, "Ottawa Tools: Place Legend on Sheets");
        transaction.Start();
        try
        {
            foreach (var sheet in window.SelectedSheets)
            {
                var position = window.PositionMethod == LegendPositionMethod.CopyFromReferenceSheet
                    ? sharedPosition
                    : LegendPlacerEngine.TitleBlockCornerPosition(doc, sheet.Id, window.Corner, window.OffsetXFeet, window.OffsetYFeet);

                if (position is null || !Viewport.CanAddViewToSheet(doc, sheet.Id, window.SelectedLegend.Id))
                {
                    skipped++;
                    continue;
                }

                Viewport.Create(doc, sheet.Id, window.SelectedLegend.Id, position);
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
            "Ottawa Tools — LegendPlacer",
            $"Placed the legend on {placed} sheet(s).{(skipped > 0 ? $" Skipped {skipped} (no title block found, or already placed there)." : "")}");

        return Result.Succeeded;
    }
}
