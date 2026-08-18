using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.LegendUsageAudit;

/// <summary>
/// Reports usage counts for every loaded detail component type — how many
/// times each is actually placed in the model — so you know what belongs
/// in a legend and what's loaded but never used. Line pattern and fill
/// pattern usage isn't reliably traceable back to "what uses this" via the
/// public API (they're referenced indirectly through subcategories and
/// materials), so this covers detail components, the part legends are
/// usually built from anyway.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "legendusageaudit";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var types = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_DetailComponents)
            .WhereElementIsElementType()
            .ToList();

        if (types.Count == 0)
        {
            TaskDialog.Show("BIMFlow — LegendUsageAudit", "No detail component types are loaded in this project.");
            return Result.Succeeded;
        }

        var instanceTypeCounts = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_DetailComponents)
            .WhereElementIsNotElementType()
            .GroupBy(e => e.GetTypeId())
            .ToDictionary(g => g.Key, g => g.Count());

        var rows = types
            .OrderByDescending(t => instanceTypeCounts.GetValueOrDefault(t.Id, 0))
            .Select(t =>
            {
                var count = instanceTypeCounts.GetValueOrDefault(t.Id, 0);
                var familyName = (t as FamilySymbol)?.Family.Name ?? "Unknown family";
                return new ResultRow(
                    new[] { familyName, t.Name, count.ToString(), count == 0 ? "Unused" : "" },
                    new List<ElementId> { t.Id });
            })
            .ToList();

        var unusedCount = rows.Count(r => r.Cells[2] == "0");

        var results = new ResultsListForm(
            "BIMFlow — LegendUsageAudit Results",
            $"{types.Count} detail component type(s) loaded, {unusedCount} never placed.",
            new[] { "Family", "Type", "Instances", "Flag" },
            rows,
            actionButtonText: "Select in model");

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }
}
