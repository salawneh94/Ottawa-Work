using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.ViewFilterCopy;

[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "viewfiltercopy";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var sourceView = doc.ActiveView;

        var sourceFilterIds = sourceView.GetFilters();
        if (sourceFilterIds.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Copy Filters", "The active view has no filters applied.");
            return Result.Succeeded;
        }

        var candidateViews = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate && v.Id != sourceView.Id && v.AreGraphicsOverridesAllowed())
            .OrderBy(v => v.Name)
            .ToList();

        if (candidateViews.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Copy Filters", "No other views accept graphic overrides.");
            return Result.Succeeded;
        }

        var window = new ViewPickerForm(
            "BIMFlow — Copy Filters",
            $"Copy {sourceFilterIds.Count} filter(s) from \"{sourceView.Name}\" into:",
            candidateViews);

        if (window.ShowDialog() != true)
            return Result.Cancelled;

        var targets = window.SelectedViews;
        if (targets.Count == 0) return Result.Succeeded;

        var copiedCount = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Copy View Filters");
        transaction.Start();
        try
        {
            foreach (var target in targets)
            {
                foreach (var filterId in sourceFilterIds)
                {
                    if (!target.GetFilters().Contains(filterId))
                        target.AddFilter(filterId);

                    target.SetFilterOverrides(filterId, sourceView.GetFilterOverrides(filterId));
                    target.SetIsFilterEnabled(filterId, sourceView.GetIsFilterEnabled(filterId));
                }
                copiedCount++;
            }
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — Copy Filters", $"Copied filters to {copiedCount} view(s).");
        return Result.Succeeded;
    }
}
