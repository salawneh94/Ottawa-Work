using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.SelectByCategory;

/// <summary>
/// Pick a category, select every instance in one click — active-view scope
/// normally, whole-project scope when the active view is a 3D view.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "selectbycategory";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;
        var isWholeProjectScope = view is View3D;

        var categories = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .Select(e => e.Category)
            .Where(c => c is not null)
            .GroupBy(c => c!.Name)
            .Select(g => g.First()!)
            .OrderBy(c => c.Name)
            .ToList();

        if (categories.Count == 0)
        {
            TaskDialog.Show("BIMFlow — SelectByCategory", "No selectable elements are visible in the active view.");
            return Result.Succeeded;
        }

        var input = new SimplePickerDialog(
            "BIMFlow — SelectByCategory",
            isWholeProjectScope ? "Category (whole project — active view is 3D):" : "Category (active view only):",
            categories.Select(c => c.Name).ToList());

        if (input.ShowDialog() != true || input.SelectedText is null)
            return Result.Cancelled;

        var category = categories.First(c => c.Name == input.SelectedText);

        var collector = isWholeProjectScope
            ? new FilteredElementCollector(doc)
            : new FilteredElementCollector(doc, view.Id);

        var ids = collector
            .WhereElementIsNotElementType()
            .OfCategoryId(category.Id)
            .Select(e => e.Id)
            .ToList();

        if (ids.Count == 0)
        {
            TaskDialog.Show("BIMFlow — SelectByCategory", "No elements found.");
            return Result.Succeeded;
        }

        uiDoc.Selection.SetElementIds(ids);
        TaskDialog.Show("BIMFlow — SelectByCategory", $"Selected {ids.Count} \"{category.Name}\" element(s).");
        return Result.Succeeded;
    }
}
