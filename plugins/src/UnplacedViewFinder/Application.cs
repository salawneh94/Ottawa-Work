using BIMFlow.Shared;

namespace BIMFlow.UnplacedViewFinder;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "UnplacedViewFinderButton";
    protected override string ButtonText => "UnplacedViewFinder";
    protected override string ToolTip => "Find drawing views that aren't placed on any sheet.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
