using BIMFlow.Shared;

namespace BIMFlow.QCSummary;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "QCSummaryButton";
    protected override string ButtonText => "QCSummary";
    protected override string ToolTip => "One-click QA dashboard: unbounded rooms, disconnected walls, orphaned doors, sill consistency.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
