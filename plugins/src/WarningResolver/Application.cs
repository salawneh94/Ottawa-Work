using BIMFlow.Shared;

namespace BIMFlow.WarningResolver;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "WarningResolverButton";
    protected override string ButtonText => "WarningResolver";
    protected override string ToolTip => "Triage and batch-fix common Revit warning categories.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
