using BIMFlow.Shared;

namespace BIMFlow.SelectRooms;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectRoomsButton";
    protected override string ButtonText => "Rooms";
    protected override string ToolTip => "Select every room visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
