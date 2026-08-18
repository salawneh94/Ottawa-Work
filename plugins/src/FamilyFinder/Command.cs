using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.FamilyFinder;

/// <summary>
/// Lists every loaded family with its type count and placed instance count
/// side by side, and flags families with a lot of types but few or no
/// instances placed — the families most likely padding file size for
/// nothing.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "familyfinder";
    private const int BloatTypeThreshold = 5;

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var families = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .ToList();

        if (families.Count == 0)
        {
            TaskDialog.Show("BIMFlow — FamilyFinder", "No loaded families were found in this project.");
            return Result.Succeeded;
        }

        var instanceCountByFamilyId = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Where(i => i.Symbol is not null)
            .GroupBy(i => i.Symbol.Family.Id)
            .ToDictionary(g => g.Key, g => g.Count());

        var rows = families
            .Select(f =>
            {
                var typeCount = f.GetFamilySymbolIds().Count;
                var instanceCount = instanceCountByFamilyId.GetValueOrDefault(f.Id, 0);
                var bloat = typeCount >= BloatTypeThreshold && instanceCount == 0;
                return (family: f, typeCount, instanceCount, bloat);
            })
            .OrderByDescending(t => t.bloat)
            .ThenByDescending(t => t.typeCount)
            .Select(t => new ResultRow(
                new[]
                {
                    t.family.Name,
                    t.family.FamilyCategory?.Name ?? "(none)",
                    t.typeCount.ToString(),
                    t.instanceCount.ToString(),
                    t.bloat ? "Bloat candidate — no instances placed" : "",
                },
                new List<ElementId> { t.family.Id }))
            .ToList();

        var bloatCount = rows.Count(r => r.Cells[4].Length > 0);

        var results = new ResultsListForm(
            "BIMFlow — FamilyFinder Results",
            $"{families.Count} loaded famil{(families.Count == 1 ? "y" : "ies")}, {bloatCount} flagged as bloat candidates.",
            new[] { "Family", "Category", "Types", "Instances", "Flag" },
            rows,
            actionButtonText: "Select in model");

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }
}
