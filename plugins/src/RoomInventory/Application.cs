using BIMFlow.Shared;

namespace BIMFlow.RoomInventory;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Rooms";
    protected override string ButtonInternalName => "RoomInventoryButton";
    protected override string ButtonText => "RoomInventory";
    protected override string ToolTip => "List every element inside each room, with a category breakdown.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
