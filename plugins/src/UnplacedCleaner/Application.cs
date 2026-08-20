using BIMFlow.Shared;

namespace BIMFlow.UnplacedCleaner;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "UnplacedCleanerButton";
    protected override string ButtonText => "Unplaced Cleaner";
    protected override string ToolTip => "Find unplaced rooms (never placed on a plan) and delete them in one pass.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
