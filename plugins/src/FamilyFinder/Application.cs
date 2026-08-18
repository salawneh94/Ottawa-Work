using BIMFlow.Shared;

namespace BIMFlow.FamilyFinder;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Parameters & Families";
    protected override string ButtonInternalName => "FamilyFinderButton";
    protected override string ButtonText => "FamilyFinder";
    protected override string ToolTip => "Audit loaded families by type count vs. placed instance count to find bloat.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
