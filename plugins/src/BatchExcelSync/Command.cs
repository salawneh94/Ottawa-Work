using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.BatchExcelSync;

/// <summary>
/// Opens the Batch Excel Sync workspace (scope + parameter picker + import
/// &amp; diff, all in BatchExcelSyncWindow). Export and re-import/diff are pure
/// file I/O the window handles itself; only the final commit touches the
/// model, so that happens here, in its own transaction, after the window
/// closes — same split every other BIMFlow dialog uses.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "batchexcelsync";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new BatchExcelSyncWindow(doc, uiDoc);
        window.ShowDialog();
        if (!window.Committed) return Result.Cancelled;

        int updated;
        using var transaction = new Transaction(doc, "BIMFlow: Commit Batch Excel Sync Changes");
        transaction.Start();
        try
        {
            updated = BatchExcelSyncEngine.Commit(doc, window.ApprovedRows, window.CommitScope, window.CommitColumns);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("BIMFlow — Batch Excel Sync", $"Couldn't commit changes: {ex.Message}");
            return Result.Failed;
        }

        TaskDialog.Show("BIMFlow — Batch Excel Sync", $"Updated {updated} of {window.ApprovedRows.Count} approved field(s).");
        return Result.Succeeded;
    }
}
