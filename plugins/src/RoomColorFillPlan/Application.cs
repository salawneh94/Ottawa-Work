using BIMFlow.Shared;

namespace BIMFlow.RoomColorFillPlan;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Rooms";
    protected override string ButtonInternalName => "RoomColorFillPlanButton";
    protected override string ButtonText => "RoomColorFillPlan";
    protected override string ToolTip => "Color-fill the active plan view by any room parameter.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
