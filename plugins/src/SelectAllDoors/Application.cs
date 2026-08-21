using OttawaWork.Shared;

namespace OttawaWork.SelectAllDoors;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectAllDoorsButton";
    protected override string ButtonText => "All Doors";
    protected override string ToolTip => "Select every door visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
