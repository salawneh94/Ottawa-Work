using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.ParameterMapper;

/// <summary>
/// Copies one parameter's value from a selected source element to every
/// element of a chosen category that matches an optional filter. A
/// relationship-aware mapper (room → hosted door, host → nested family)
/// needs reliable host/room lookups this pass doesn't attempt — this covers
/// the simpler, still very common "copy this value onto that set" case.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "parametermapper";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var selection = uiDoc.Selection.GetElementIds().Select(id => doc.GetElement(id)).ToList();
        if (selection.Count != 1)
        {
            TaskDialog.Show("BIMFlow — ParameterMapper", "Select exactly one source element first, then run this tool.");
            return Result.Cancelled;
        }

        var source = selection[0];

        var window = new ParameterMapperWindow(doc, source);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.ParameterName is null || window.TargetCategory is null)
            return Result.Succeeded;

        var sourceParam = source.LookupParameter(window.ParameterName);
        if (sourceParam is null)
        {
            TaskDialog.Show("BIMFlow — ParameterMapper", "That parameter wasn't found on the source element.");
            return Result.Cancelled;
        }

        if (sourceParam.StorageType is not (StorageType.String or StorageType.Integer or StorageType.Double))
        {
            TaskDialog.Show("BIMFlow — ParameterMapper", "This tool copies text, number, and integer parameters — not element references.");
            return Result.Cancelled;
        }

        var targets = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfCategoryId(window.TargetCategory.Id)
            .Where(e => e.Id != source.Id)
            .Where(e => window.FilterRules.All(r => r.Matches(e)))
            .ToList();

        if (targets.Count == 0)
        {
            TaskDialog.Show("BIMFlow — ParameterMapper", "No target elements matched.");
            return Result.Succeeded;
        }

        var updated = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Map Parameter Value");
        transaction.Start();
        try
        {
            foreach (var target in targets)
            {
                var targetParam = target.LookupParameter(window.ParameterName);
                if (targetParam is not { IsReadOnly: false } || targetParam.StorageType != sourceParam.StorageType)
                    continue;

                switch (sourceParam.StorageType)
                {
                    case StorageType.String:
                        targetParam.Set(sourceParam.AsString() ?? string.Empty);
                        break;
                    case StorageType.Integer:
                        targetParam.Set(sourceParam.AsInteger());
                        break;
                    case StorageType.Double:
                        targetParam.Set(sourceParam.AsDouble());
                        break;
                }
                updated++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — ParameterMapper", $"Copied \"{window.ParameterName}\" to {updated} of {targets.Count} target element(s).");
        return Result.Succeeded;
    }
}
