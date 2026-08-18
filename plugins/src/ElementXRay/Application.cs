using BIMFlow.Shared;

namespace BIMFlow.ElementXRay;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "ElementXRayButton";
    protected override string ButtonText => "ElementX-Ray";
    protected override string ToolTip => "Diagnose why a selected element isn't visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
