using BIMFlow.Shared;

namespace BIMFlow.GridGenerator;

public class Application : BimFlowApplication
{
    protected override string PanelName => "MEP & Structural";
    protected override string ButtonInternalName => "GridGeneratorButton";
    protected override string ButtonText => "GridGenerator";
    protected override string ToolTip => "Auto-generate structural grids from CAD or Excel input.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
