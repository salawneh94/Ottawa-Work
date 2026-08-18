using BIMFlow.Shared;

namespace BIMFlow.WorksetMonitor;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Data & Coordination";
    protected override string ButtonInternalName => "WorksetMonitorButton";
    protected override string ButtonText => "WorksetMonitor";
    protected override string ToolTip => "Live dashboard of workset ownership, sync times, and element counts.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
