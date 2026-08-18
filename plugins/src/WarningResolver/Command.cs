using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.WarningResolver;

/// <summary>
/// Groups every warning in the model by its message text and severity, so
/// you can triage the categories that matter instead of scrolling
/// Revit's flat warnings dialog. Scoped to reporting — most warning types
/// don't have a single safe automated fix, so this doesn't attempt one.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "warningresolver";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var warnings = doc.GetWarnings();
        if (warnings.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Warning Resolver", "This model has no warnings.");
            return Result.Succeeded;
        }

        var groups = warnings
            .GroupBy(w => w.GetDescriptionText())
            .Select(g => new WarningGroup(
                g.Key,
                g.First().GetSeverity(),
                g.SelectMany(w => w.GetFailingElements()).Distinct().ToList()))
            .ToList();

        var window = new WarningResultsWindow(groups);
        if (window.ShowDialog() == true && window.ElementsToSelect.Count > 0)
        {
            uiDoc.Selection.SetElementIds(window.ElementsToSelect);
            uiDoc.ShowElements(window.ElementsToSelect);
        }

        return Result.Succeeded;
    }
}
