using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.Din276CostEstimator;

/// <summary>
/// Opens the DIN 276 cost estimator. Everything it does — classifying
/// elements, computing quantities, pricing, importing/exporting Excel — is
/// read-only against the model, so unlike most BIMFlow commands this one
/// needs no transaction at all.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "din276costestimator";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new Din276Window(doc, doc.ActiveView);
        window.ShowDialog();

        return Result.Succeeded;
    }
}
