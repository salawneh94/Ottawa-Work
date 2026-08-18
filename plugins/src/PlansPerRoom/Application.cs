using BIMFlow.Shared;

namespace BIMFlow.PlansPerRoom;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Rooms";
    protected override string ButtonInternalName => "PlansPerRoomButton";
    protected override string ButtonText => "PlansPerRoom";
    protected override string ToolTip => "Generate a cropped floor plan view for every room in one pass.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
