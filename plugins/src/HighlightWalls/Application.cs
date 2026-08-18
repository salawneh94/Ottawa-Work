using BIMFlow.Shared;

namespace BIMFlow.HighlightWalls;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Highlight";
    protected override string ButtonInternalName => "HighlightWallsButton";
    protected override string ButtonText => "HighlightWalls";
    protected override string ToolTip => "Toggle an interior/exterior color highlight on every wall in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
