using OttawaWork.Shared;

namespace OttawaWork.SelectAllWalls;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectAllWallsButton";
    protected override string ButtonText => "All Walls";
    protected override string ToolTip => "Select every wall visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
