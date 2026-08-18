using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.GridBubbleManager;

[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "gridbubblemanager";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var candidateViews = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate && (v.ViewType == ViewType.FloorPlan
                                           || v.ViewType == ViewType.CeilingPlan
                                           || v.ViewType == ViewType.Elevation
                                           || v.ViewType == ViewType.Section))
            .OrderBy(v => v.Name)
            .ToList();

        var window = new GridBubbleOptionsWindow(doc.ActiveView, candidateViews);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedViews.Count == 0 || (!window.IncludeGrids && !window.IncludeLevels))
            return Result.Succeeded;

        var datums = new List<DatumPlane>();
        if (window.IncludeGrids)
            datums.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<DatumPlane>());
        if (window.IncludeLevels)
            datums.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<DatumPlane>());

        var changed = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Set Grid/Level Bubbles");
        transaction.Start();
        try
        {
            foreach (var view in window.SelectedViews)
            {
                foreach (var datum in datums)
                {
                    if (!datum.CanBeVisibleInView(view)) continue;

                    ApplyBubbleState(datum, DatumEnds.End0, window.ShowStartBubble, view);
                    ApplyBubbleState(datum, DatumEnds.End1, window.ShowEndBubble, view);
                    changed++;
                }
            }
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — Grid Bubble Manager", $"Updated bubble visibility on {changed} datum/view combination(s).");
        return Result.Succeeded;
    }

    private static void ApplyBubbleState(DatumPlane datum, DatumEnds end, bool show, View view)
    {
        if (!datum.IsBubbleVisibleInView(end, view) && show)
            datum.ShowBubbleInView(end, view);
        else if (datum.IsBubbleVisibleInView(end, view) && !show)
            datum.HideBubbleInView(end, view);
    }
}
