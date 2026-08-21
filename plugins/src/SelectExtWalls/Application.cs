using BIMFlow.Shared;

namespace BIMFlow.SelectExtWalls;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectExtWallsButton";
    protected override string ButtonText => "Ext Walls";
    protected override string ToolTip => "Select every exterior wall visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
