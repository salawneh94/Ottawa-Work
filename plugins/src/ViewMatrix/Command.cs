using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.ViewMatrix;

/// <summary>
/// Lists scope box and crop region state for every view side by side, and
/// flags views missing a scope box that most of their same-type peers have
/// — the kind of inconsistency that's invisible scrolling the project
/// browser one view at a time.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "viewmatrix";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var views = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate)
            .ToList();

        if (views.Count == 0)
        {
            TaskDialog.Show("BIMFlow — ViewMatrix", "No views were found in this project.");
            return Result.Succeeded;
        }

        var scopeBoxByView = new Dictionary<ElementId, string?>();
        foreach (var view in views)
        {
            var param = view.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
            var scopeBoxId = param?.AsElementId();
            scopeBoxByView[view.Id] = scopeBoxId is { } id && id != ElementId.InvalidElementId
                ? doc.GetElement(id)?.Name
                : null;
        }

        var groupHasScopeBoxRate = views
            .GroupBy(v => v.ViewType)
            .ToDictionary(g => g.Key, g => g.Count(v => scopeBoxByView[v.Id] is not null) / (double)g.Count());

        var rows = views
            .OrderBy(v => v.ViewType.ToString())
            .ThenBy(v => v.Name)
            .Select(v =>
            {
                var scopeBox = scopeBoxByView[v.Id];
                var flagged = scopeBox is null && groupHasScopeBoxRate.GetValueOrDefault(v.ViewType, 0) > 0.5;
                return new ResultRow(
                    new[]
                    {
                        v.Name,
                        v.ViewType.ToString(),
                        scopeBox ?? "(none)",
                        v.CropBoxActive ? "Active" : "Inactive",
                        flagged ? "Missing scope box (peers have one)" : "",
                    },
                    new List<ElementId> { v.Id });
            })
            .ToList();

        var flaggedCount = rows.Count(r => r.Cells[4].Length > 0);

        var results = new ResultsListForm(
            "BIMFlow — ViewMatrix Results",
            $"{views.Count} view(s) audited, {flaggedCount} flagged.",
            new[] { "View", "Type", "Scope Box", "Crop", "Flag" },
            rows,
            actionButtonText: "Select in model");

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }
}
