using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.HighlightDashboard;

/// <summary>
/// Opens the Highlight dashboard, then — if the user picked a card — posts
/// that tool's own already-registered command via
/// UIApplication.PostCommand(RevitCommandId.LookupCommandId(fullClassName))
/// rather than calling its Command class directly. Posting defers execution
/// until after this command's own Execute() returns, which is the correct,
/// Revit-idiomatic way for one add-in's UI to trigger another registered
/// external command — it avoids re-entering another command's Execute
/// while this one is still mid-flight, and it's what makes "zero code
/// duplication" literal rather than aspirational: this class contains no
/// highlight/select/color-code logic of its own, at all.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "highlightdashboard";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var window = new HighlightDashboardWindow();
        window.ShowDialog();
        if (window.SelectedCommandFullClassName is null) return Result.Succeeded;

        var uiApp = commandData.Application;
        var commandId = RevitCommandId.LookupCommandId(window.SelectedCommandFullClassName);
        if (commandId is not null && uiApp.CanPostCommand(commandId))
            uiApp.PostCommand(commandId);

        return Result.Succeeded;
    }
}
