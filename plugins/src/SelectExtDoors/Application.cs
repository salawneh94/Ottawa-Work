using OttawaWork.Shared;

namespace OttawaWork.SelectExtDoors;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectExtDoorsButton";
    protected override string ButtonText => "Ext Doors";
    protected override string ToolTip => "Select every exterior door visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
