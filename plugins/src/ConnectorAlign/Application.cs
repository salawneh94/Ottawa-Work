using BIMFlow.Shared;

namespace BIMFlow.ConnectorAlign;

public class Application : BimFlowApplication
{
    protected override string PanelName => "MEP & Structural";
    protected override string ButtonInternalName => "ConnectorAlignButton";
    protected override string ButtonText => "ConnectorAlign";
    protected override string ToolTip => "Detect and fix misaligned or disconnected MEP connectors.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
