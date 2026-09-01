using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.StairCalculator;

/// <summary>
/// Opens the Stair Calculator dialog: a Design Calculator that searches
/// riser/tread combinations against DIN 18065's reference thresholds for a
/// given floor-to-floor rise, and an Audit mode that checks every real
/// Stairs element already in the model against the same rules. A design-
/// proportion check, not a certified code compliance review — actual DIN
/// 18065 wording and any local building-code overlay should still be
/// verified for the specific project.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "staircalculator";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new StairCalculatorWindow(doc);
        if (window.ShowDialog() == true && window.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(window.ElementsToSelect);

        return Result.Succeeded;
    }
}
