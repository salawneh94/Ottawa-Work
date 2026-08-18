using BIMFlow.Shared;

namespace BIMFlow.OverriddenDimensions;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "OverriddenDimensionsButton";
    protected override string ButtonText => "OverriddenDimensions";
    protected override string ToolTip => "Find dimensions with a manually typed value override.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
