using BIMFlow.Shared;

namespace BIMFlow.FramingChecker;

public class Application : BimFlowApplication
{
    protected override string PanelName => "MEP & Structural";
    protected override string ButtonInternalName => "FramingCheckerButton";
    protected override string ButtonText => "FramingChecker";
    protected override string ToolTip => "Validate framing member connections, orientation, and cuts.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
