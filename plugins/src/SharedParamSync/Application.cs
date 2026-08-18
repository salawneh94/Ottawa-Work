using BIMFlow.Shared;

namespace BIMFlow.SharedParamSync;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Parameters & Families";
    protected override string ButtonInternalName => "SharedParamSyncButton";
    protected override string ButtonText => "Shared Param Sync";
    protected override string ToolTip => "Diff and merge two shared parameter files.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
