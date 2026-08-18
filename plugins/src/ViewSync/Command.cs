using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.ViewSync;

[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "viewsync";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var templates = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.IsTemplate)
            .OrderBy(v => v.Name)
            .ToList();

        if (templates.Count == 0)
        {
            TaskDialog.Show("BIMFlow — ViewSync", "No view templates were found in this project.");
            return Result.Succeeded;
        }

        var candidateViews = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate)
            .OrderBy(v => v.Name)
            .ToList();

        var window = new ViewSyncWindow(templates, candidateViews);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedTemplate is null || window.SelectedViews.Count == 0)
            return Result.Succeeded;

        var compatible = window.SelectedViews.Where(v => v.ViewType == window.SelectedTemplate.ViewType).ToList();
        var skipped = window.SelectedViews.Count - compatible.Count;

        using var transaction = new Transaction(doc, "BIMFlow: Apply View Template");
        transaction.Start();
        try
        {
            foreach (var view in compatible)
                view.ViewTemplateId = window.SelectedTemplate.Id;

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        var summary = $"Applied \"{window.SelectedTemplate.Name}\" to {compatible.Count} view(s).";
        if (skipped > 0)
            summary += $"\n\n{skipped} view(s) were skipped — different view type than the template.";

        TaskDialog.Show("BIMFlow — ViewSync", summary);
        return Result.Succeeded;
    }
}
