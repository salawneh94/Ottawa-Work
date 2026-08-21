using BIMFlow.Shared;

namespace BIMFlow.SelectIntDoors;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectIntDoorsButton";
    protected override string ButtonText => "Int Doors";
    protected override string ToolTip => "Select every interior door visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
