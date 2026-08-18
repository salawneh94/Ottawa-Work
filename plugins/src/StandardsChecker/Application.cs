using BIMFlow.Shared;

namespace BIMFlow.StandardsChecker;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "StandardsCheckerButton";
    protected override string ButtonText => "StandardsChecker";
    protected override string ToolTip => "Rule-based QA/QC audit against your firm's BIM standard.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
