using OttawaWork.Shared;

namespace OttawaWork.SelectFloors;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectFloorsButton";
    protected override string ButtonText => "Floors";
    protected override string ToolTip => "Select every floor visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
