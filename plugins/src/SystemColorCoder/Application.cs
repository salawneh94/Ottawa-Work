using BIMFlow.Shared;

namespace BIMFlow.SystemColorCoder;

public class Application : BimFlowApplication
{
    protected override string PanelName => "MEP & Structural";
    protected override string ButtonInternalName => "SystemColorCoderButton";
    protected override string ButtonText => "SystemColorCoder";
    protected override string ToolTip => "Auto color-code MEP systems by type and zone across every view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
