using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.PurgePro;

/// <summary>
/// Launches Revit's own Purge Unused command (still an interactive dialog —
/// the public API doesn't expose a headless purge) and, once it closes,
/// reports how many elements were removed and offers to run it again, since
/// a second or third pass routinely finds more once containers empty out.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "purgepro";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var session = new PurgeSession(commandData.Application);
        if (!session.TryStart(out var reason))
        {
            TaskDialog.Show("BIMFlow — Purge Pro", reason);
            return Result.Cancelled;
        }

        return Result.Succeeded;
    }
}

internal sealed class PurgeSession
{
    private readonly UIApplication _uiApp;
    private readonly Document _doc;
    private readonly RevitCommandId? _commandId;
    private int _beforeCount;

    public PurgeSession(UIApplication uiApp)
    {
        _uiApp = uiApp;
        _doc = uiApp.ActiveUIDocument.Document;
        _commandId = RevitCommandId.LookupPostableCommandId(PostableCommand.PurgeUnused);
    }

    public bool TryStart(out string reason)
    {
        if (_commandId is null || !_uiApp.CanPostCommand(_commandId))
        {
            reason = "Purge Unused isn't available right now (it may already be running, or this document doesn't support it).";
            return false;
        }

        RunPass();
        reason = string.Empty;
        return true;
    }

    private void RunPass()
    {
        _beforeCount = CountElements();
        _uiApp.Application.DocumentChanged += OnDocumentChanged;
        _uiApp.PostCommand(_commandId);
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        _uiApp.Application.DocumentChanged -= OnDocumentChanged;

        var removed = _beforeCount - CountElements();

        var dialog = new TaskDialog("BIMFlow — Purge Pro")
        {
            MainInstruction = removed > 0 ? $"Removed {removed} element(s)." : "No unused elements were removed.",
            MainContent = removed > 0
                ? "A second pass often finds more, since some elements are only orphaned once their container is purged."
                : "The model is already clean, or the dialog was closed without purging.",
            CommonButtons = TaskDialogCommonButtons.Close,
        };

        if (removed > 0)
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Run Purge Pro again");

        if (dialog.Show() == TaskDialogResult.CommandLink1)
            RunPass();
    }

    private int CountElements() =>
        new FilteredElementCollector(_doc).WhereElementIsNotElementType().GetElementCount();
}
