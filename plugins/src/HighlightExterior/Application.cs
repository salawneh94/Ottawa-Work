using BIMFlow.Shared;

namespace BIMFlow.HighlightExterior;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Highlight";
    protected override string ButtonInternalName => "HighlightExteriorButton";
    protected override string ButtonText => "HL Exterior";
    protected override string ToolTip => "Toggle a red color highlight on every exterior wall in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
