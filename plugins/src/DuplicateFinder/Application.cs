using BIMFlow.Shared;

namespace BIMFlow.DuplicateFinder;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "DuplicateFinderButton";
    protected override string ButtonText => "Duplicate Finder";
    protected override string ToolTip => "Find overlapping or duplicate elements in the model.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
