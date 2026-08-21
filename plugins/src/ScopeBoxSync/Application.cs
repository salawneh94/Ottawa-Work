using OttawaWork.Shared;

namespace OttawaWork.ScopeBoxSync;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "ScopeBoxSyncButton";
    protected override string ButtonText => "Scope Box Sync";
    protected override string ToolTip => "Apply a scope box to a batch of selected views.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
