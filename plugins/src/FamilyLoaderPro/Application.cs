using BIMFlow.Shared;

namespace BIMFlow.FamilyLoaderPro;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Parameters & Families";
    protected override string ButtonInternalName => "FamilyLoaderProButton";
    protected override string ButtonText => "FamilyLoaderPro";
    protected override string ToolTip => "Batch-load every family in a folder, with conflict handling.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
