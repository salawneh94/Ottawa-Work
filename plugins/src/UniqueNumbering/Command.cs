using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.UniqueNumbering;

/// <summary>
/// Batch-assigns sequential values (Room Number, door Mark, Comments, or any
/// other String-storage parameter on the chosen category) using one or more
/// rules the user builds in UniqueNumberingWindow, applied against elements
/// in whatever sort order the window computed. Actual writes happen here,
/// in a transaction, against exactly the preview rows the user already
/// reviewed in the window — nothing is re-derived from live model state
/// between "Generate Preview" and "Assign", so what gets written always
/// matches what was shown.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "uniquenumbering";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var selectionIds = uiDoc.Selection.GetElementIds();

        var window = new UniqueNumberingWindow(doc, selectionIds);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        return window.ChosenAction switch
        {
            UniqueNumberingAction.Assign => Assign(doc, window),
            UniqueNumberingAction.ClearValues => ClearValues(doc, window),
            _ => Result.Succeeded,
        };
    }

    private static Result Assign(Document doc, UniqueNumberingWindow window)
    {
        using var transaction = new Transaction(doc, "Ottawa Tools: Unique Numbering");
        transaction.Start();
        int applied;
        try
        {
            applied = UniqueNumberingEngine.Apply(doc, window.PreviewRows, window.Rules);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("Ottawa Tools — Unique Numbering", $"Couldn't assign numbers: {ex.Message}");
            return Result.Failed;
        }

        TaskDialog.Show("Ottawa Tools — Unique Numbering", $"Assigned {applied} value(s) across {window.PreviewRows.Count} element(s).");
        return Result.Succeeded;
    }

    private static Result ClearValues(Document doc, UniqueNumberingWindow window)
    {
        using var transaction = new Transaction(doc, "Ottawa Tools: Clear Unique Numbering Values");
        transaction.Start();
        var cleared = 0;
        try
        {
            foreach (var row in window.PreviewRows)
            {
                if (doc.GetElement(row.ElementId) is not { } element) continue;

                foreach (var rule in window.Rules)
                {
                    var parameter = element.LookupParameter(rule.ParameterName);
                    if (parameter is not { StorageType: StorageType.String, IsReadOnly: false }) continue;

                    parameter.Set("");
                    cleared++;
                }
            }
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("Ottawa Tools — Unique Numbering", $"Couldn't clear values: {ex.Message}");
            return Result.Failed;
        }

        TaskDialog.Show("Ottawa Tools — Unique Numbering", $"Cleared {cleared} value(s).");
        return Result.Succeeded;
    }
}
