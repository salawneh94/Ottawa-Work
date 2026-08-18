using BIMFlow.Shared;

namespace BIMFlow.SlabHeightSync;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Rooms";
    protected override string ButtonInternalName => "SlabHeightSyncButton";
    protected override string ButtonText => "SlabHeightSync";
    protected override string ToolTip => "Batch-set Height Offset From Level across a set of floors.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
