using BIMFlow.Shared;

namespace BIMFlow.RoomFinisher;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "RoomFinisherButton";
    protected override string ButtonText => "RoomFinisher";
    protected override string ToolTip => "Find unplaced, unenclosed, and unbounded rooms before they bite you.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
