using BIMFlow.Shared;

namespace BIMFlow.DimensionAuto;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "DimensionAutoButton";
    protected override string ButtonText => "DimensionAuto";
    protected override string ToolTip => "Auto-place dimension strings on walls, grids, and openings.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
