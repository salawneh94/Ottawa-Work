using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.QuickSelect;

[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "quickselectplus";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new QuickSelectWindow(doc);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedCategory is null)
        {
            TaskDialog.Show("BIMFlow — QuickSelect+", "No category was selected.");
            return Result.Succeeded;
        }

        var matches = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfCategoryId(window.SelectedCategory.Id)
            .Where(e => window.Rules.All(r => r.Matches(e)))
            .Select(e => e.Id)
            .ToList();

        if (matches.Count == 0)
        {
            TaskDialog.Show("BIMFlow — QuickSelect+", "No elements matched those rules.");
            return Result.Succeeded;
        }

        uiDoc.Selection.SetElementIds(matches);
        TaskDialog.Show("BIMFlow — QuickSelect+", $"Selected {matches.Count} element(s).");
        return Result.Succeeded;
    }
}
