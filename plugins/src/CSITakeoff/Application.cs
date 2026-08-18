using BIMFlow.Shared;

namespace BIMFlow.CSITakeoff;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Data & Coordination";
    protected override string ButtonInternalName => "CSITakeoffButton";
    protected override string ButtonText => "CSITakeoff";
    protected override string ToolTip => "Quantity takeoff grouped by Assembly Code, exported to CSV.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
