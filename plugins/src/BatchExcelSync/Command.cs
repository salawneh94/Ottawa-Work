using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.BatchExcelSync;

/// <summary>
/// Opens the Batch Excel Sync workspace (scope + parameter picker + import
/// &amp; diff, all in BatchExcelSyncWindow). Export and re-import/diff are pure
/// file I/O the window handles itself; only the final commit touches the
/// model, so that happens here, in its own transaction, after the window
/// closes — same split every other Ottawa Tools dialog uses.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "batchexcelsync";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new BatchExcelSyncWindow(doc, uiDoc);
        window.ShowDialog();
        if (!window.Committed) return Result.Cancelled;

        BatchExcelSyncEngine.CommitResult result;
        using var transaction = new Transaction(doc, "Ottawa Tools: Commit Batch Excel Sync Changes");
        transaction.Start();
        try
        {
            result = BatchExcelSyncEngine.Commit(doc, window.ApprovedRows, window.CommitScope, window.CommitColumns);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("Ottawa Tools — Batch Excel Sync", $"Couldn't commit changes: {ex.Message}");
            return Result.Failed;
        }

        var summary = $"Updated {result.Updated} of {window.ApprovedRows.Count} approved field(s).";
        if (result.Failures.Count > 0)
        {
            summary += "\n\nWhy some fields didn't update:";
            foreach (var group in result.Failures.GroupBy(f => f.Reason).OrderByDescending(g => g.Count()).Take(5))
                summary += $"\n• {group.Count()}x — {group.Key} (e.g. \"{group.First().NewValue}\" → {group.First().ParamName} on {group.First().ElementLabel})";
        }
        TaskDialog.Show("Ottawa Tools — Batch Excel Sync", summary);
        return Result.Succeeded;
    }
}
