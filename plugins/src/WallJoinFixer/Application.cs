using BIMFlow.Shared;

namespace BIMFlow.WallJoinFixer;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "WallJoinFixerButton";
    protected override string ButtonText => "Wall Join Fixer";
    protected override string ToolTip => "Batch-set wall join types and re-join nearby wall geometry.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
