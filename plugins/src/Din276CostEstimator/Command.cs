using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.Din276CostEstimator;

/// <summary>
/// Opens the DIN 276 cost estimator. Classifying elements, computing
/// quantities, pricing, and importing/exporting Excel are all read-only
/// against the model and handled entirely inside Din276Window. "Assign to
/// elements" is the one action that writes to the model — the window only
/// confirms and hands back which elements resolved to which Kostengruppe
/// code (window.PendingAssignments); the actual writes happen here, in
/// their own transaction with rollback on failure, same split every other
/// Ottawa Tools dialog that can modify the model uses.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "din276costestimator";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new Din276Window(doc, doc.ActiveView);
        window.ShowDialog();
        if (!window.AssignRequested) return Result.Succeeded;

        var assigned = 0;
        var skipped = 0;

        using var transaction = new Transaction(doc, "Assign DIN 276 Kostengruppen");
        transaction.Start();
        try
        {
            Din276Engine.EnsureKostengruppeParameter(doc);

            foreach (var quantity in window.PendingAssignments)
            {
                var element = doc.GetElement(quantity.ElementId);
                if (element is not null && Din276Engine.TryAssignKostengruppe(element, quantity.Code))
                    assigned++;
                else
                    skipped++;
            }
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("Ottawa Tools — DIN 276 Cost Estimator", $"Couldn't assign Kostengruppen: {ex.Message}");
            return Result.Failed;
        }

        TaskDialog.Show(
            "Ottawa Tools — DIN 276 Cost Estimator",
            $"Assigned {assigned} element(s).\nSkipped {skipped} element(s) with no writable parameter available.");

        return Result.Succeeded;
    }
}
