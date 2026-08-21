using BIMFlow.Shared;

namespace BIMFlow.SelectExtDoors;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectExtDoorsButton";
    protected override string ButtonText => "Ext Doors";
    protected override string ToolTip => "Select every exterior door visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
