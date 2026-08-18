using BIMFlow.Shared;

namespace BIMFlow.ModelHealthDashboard;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "ModelHealthDashboardButton";
    protected override string ButtonText => "ModelHealthDashboard";
    protected override string ToolTip => "Track file size, warning count, and element count trends over time.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
