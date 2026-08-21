using OttawaWork.Shared;

namespace OttawaWork.SelectIntDoors;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectIntDoorsButton";
    protected override string ButtonText => "Int Doors";
    protected override string ToolTip => "Select every interior door visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
