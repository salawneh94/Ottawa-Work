using OttawaWork.Shared;

namespace OttawaWork.SelectRoofs;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectRoofsButton";
    protected override string ButtonText => "Roofs";
    protected override string ToolTip => "Select every roof visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
