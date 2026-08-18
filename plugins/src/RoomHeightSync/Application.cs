using BIMFlow.Shared;

namespace BIMFlow.RoomHeightSync;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Rooms";
    protected override string ButtonInternalName => "RoomHeightSyncButton";
    protected override string ButtonText => "RoomHeightSync";
    protected override string ToolTip => "Batch-set room computation heights from a CSV.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
