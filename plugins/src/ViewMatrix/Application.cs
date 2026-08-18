using BIMFlow.Shared;

namespace BIMFlow.ViewMatrix;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "ViewMatrixButton";
    protected override string ButtonText => "ViewMatrix";
    protected override string ToolTip => "Audit scope box and crop region settings across every view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
