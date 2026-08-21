using BIMFlow.Shared;

namespace BIMFlow.HighlightDashboard;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Highlight";
    protected override string ButtonInternalName => "HighlightDashboardButton";
    protected override string ButtonText => "Highlight";
    protected override string ToolTip => "Browse every Select/Highlight tool in one card grid and launch any of them from here.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
