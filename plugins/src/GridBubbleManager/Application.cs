using BIMFlow.Shared;

namespace BIMFlow.GridBubbleManager;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "GridBubbleManagerButton";
    protected override string ButtonText => "Grid Bubbles";
    protected override string ToolTip => "Batch toggle grid and level bubble visibility.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
