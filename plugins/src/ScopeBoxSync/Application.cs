using BIMFlow.Shared;

namespace BIMFlow.ScopeBoxSync;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "ScopeBoxSyncButton";
    protected override string ButtonText => "Scope Box Sync";
    protected override string ToolTip => "Apply a scope box to a batch of selected views.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
