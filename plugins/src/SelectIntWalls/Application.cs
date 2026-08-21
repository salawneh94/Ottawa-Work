using OttawaWork.Shared;

namespace OttawaWork.SelectIntWalls;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectIntWallsButton";
    protected override string ButtonText => "Int Walls";
    protected override string ToolTip => "Select every interior wall visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
