using BIMFlow.Shared;

namespace BIMFlow.InsulationManager;

public class Application : BimFlowApplication
{
    protected override string PanelName => "MEP & Structural";
    protected override string ButtonInternalName => "InsulationManagerButton";
    protected override string ButtonText => "InsulationManager";
    protected override string ToolTip => "Batch-apply and update pipe/duct insulation by system rule.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
