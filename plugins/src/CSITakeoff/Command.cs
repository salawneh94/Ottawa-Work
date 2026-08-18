using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.CSITakeoff;

/// <summary>
/// Groups every model element by its type's Assembly Code (Revit's
/// built-in UniFormat/MasterFormat-style classification field) and sums
/// counts, then exports the breakdown to a branded Excel workbook.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "csitakeoff";

    private record Group(string Code, string Description);

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e.Category is { CategoryType: CategoryType.Model })
            .ToList();

        if (elements.Count == 0)
        {
            TaskDialog.Show("BIMFlow — CSITakeoff", "No model elements were found in this project.");
            return Result.Succeeded;
        }

        var counts = new Dictionary<Group, int>();
        var uncoded = 0;

        foreach (var element in elements)
        {
            var typeElement = doc.GetElement(element.GetTypeId());
            var code = typeElement?.get_Parameter(BuiltInParameter.UNIFORMAT_CODE)?.AsString();

            if (string.IsNullOrWhiteSpace(code))
            {
                uncoded++;
                continue;
            }

            var description = typeElement?.get_Parameter(BuiltInParameter.UNIFORMAT_DESCRIPTION)?.AsString() ?? "";
            var group = new Group(code, description);
            counts[group] = counts.GetValueOrDefault(group) + 1;
        }

        if (counts.Count == 0)
        {
            TaskDialog.Show("BIMFlow — CSITakeoff", "None of the model elements' types have an Assembly Code set.");
            return Result.Succeeded;
        }

        var rows = counts.OrderBy(kv => kv.Key.Code)
            .Select(kv => new[] { kv.Key.Code, kv.Key.Description, kv.Value.ToString() })
            .ToList();

        var path = BrandedXlsx.Save(
            "Export CSI takeoff",
            "csi-takeoff.xlsx",
            "CSI Takeoff",
            $"{doc.Title} — Quantity Takeoff by Assembly Code",
            new[] { "Assembly Code", "Assembly Description", "Count" },
            rows);
        if (path is null) return Result.Cancelled;

        TaskDialog.Show(
            "BIMFlow — CSITakeoff",
            $"Exported {counts.Count} assembly code group(s) covering {elements.Count - uncoded} element(s) to:\n{path}\n\n" +
            $"{uncoded} element(s) had no Assembly Code set and were excluded.");

        return Result.Succeeded;
    }
}
