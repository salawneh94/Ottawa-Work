using BIMFlow.Shared;

namespace BIMFlow.SelectIntWalls;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectIntWallsButton";
    protected override string ButtonText => "Int Walls";
    protected override string ToolTip => "Select every interior wall visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
