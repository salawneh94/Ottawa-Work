using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.ParameterFormulaPropagator;

/// <summary>
/// Fills a text parameter across a filtered set of elements from a
/// template with {Level}, {Category}, and {seq}/{seq:000} placeholders —
/// e.g. "{Level}-EQ-{seq:000}" numbers every match per-level with a
/// zero-padded sequence.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "parameterformulapropagator";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new PropagatorWindow(doc);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedCategory is null || window.TargetParameterName is null)
            return Result.Succeeded;

        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfCategoryId(window.SelectedCategory.Id)
            .Where(e => window.FilterRules.All(r => r.Matches(e)))
            .OrderBy(e => doc.GetElement(e.LevelId)?.Name ?? "")
            .ThenBy(e => e.Id.Value)
            .ToList();

        if (elements.Count == 0)
        {
            TaskDialog.Show("BIMFlow — ParameterFormulaPropagator", "No elements matched that filter.");
            return Result.Succeeded;
        }

        var updated = 0;
        var sequence = window.StartNumber;

        using var transaction = new Transaction(doc, "BIMFlow: Propagate Parameter Formula");
        transaction.Start();
        try
        {
            foreach (var element in elements)
            {
                var param = element.LookupParameter(window.TargetParameterName);
                if (param is not { IsReadOnly: false, StorageType: StorageType.String }) continue;

                var value = TemplateResolver.Resolve(window.Template, element, doc, sequence);
                param.Set(value);
                sequence++;
                updated++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — ParameterFormulaPropagator", $"Set \"{window.TargetParameterName}\" on {updated} element(s).");
        return Result.Succeeded;
    }
}
