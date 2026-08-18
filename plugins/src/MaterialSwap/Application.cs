using BIMFlow.Shared;

namespace BIMFlow.MaterialSwap;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "MaterialSwapButton";
    protected override string ButtonText => "MaterialSwap";
    protected override string ToolTip => "Batch-swap a material across every compound-structure layer that uses it.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
