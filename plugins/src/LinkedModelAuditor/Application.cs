using BIMFlow.Shared;

namespace BIMFlow.LinkedModelAuditor;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Data & Coordination";
    protected override string ButtonInternalName => "LinkedModelAuditorButton";
    protected override string ButtonText => "LinkedModelAuditor";
    protected override string ToolTip => "One dashboard for every linked model's status, worksets, and positioning.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
