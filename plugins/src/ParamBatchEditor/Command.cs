using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.ParamBatchEditor;

/// <summary>
/// Bulk-sets one parameter to one value across every element of a category
/// that matches up to two filter rules. A full spreadsheet-style grid with
/// per-row fill-down/find-replace is a materially bigger UI project — this
/// covers the single most common case (set X to Y for everything matching
/// a filter) in one pass.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "parambatcheditor";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new ParamBatchEditorWindow(doc);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedCategory is null || window.TargetParameterName is null)
            return Result.Succeeded;

        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfCategoryId(window.SelectedCategory.Id)
            .Where(e => window.FilterRules.All(r => r.Matches(e)))
            .ToList();

        if (elements.Count == 0)
        {
            TaskDialog.Show("BIMFlow — ParamBatchEditor", "No elements matched that filter.");
            return Result.Succeeded;
        }

        var updated = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Batch Edit Parameter");
        transaction.Start();
        try
        {
            foreach (var element in elements)
            {
                var param = element.LookupParameter(window.TargetParameterName);
                if (param is not { IsReadOnly: false }) continue;

                try
                {
                    SetParameterValue(param, window.NewValue);
                    updated++;
                }
                catch (Exception)
                {
                    // Value doesn't fit the parameter's type (e.g. non-numeric text into a number) — skip it.
                }
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — ParamBatchEditor", $"Updated {updated} of {elements.Count} matching element(s).");
        return Result.Succeeded;
    }

    private static void SetParameterValue(Parameter param, string value)
    {
        switch (param.StorageType)
        {
            case StorageType.String:
                param.Set(value);
                break;
            case StorageType.Integer:
                param.Set(int.Parse(value));
                break;
            case StorageType.Double:
                param.Set(double.Parse(value));
                break;
            default:
                throw new NotSupportedException("Unsupported parameter storage type for text input.");
        }
    }
}
