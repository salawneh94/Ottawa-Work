using BIMFlow.Shared;

namespace BIMFlow.MEPClashPrecheck;

public class Application : BimFlowApplication
{
    protected override string PanelName => "MEP & Structural";
    protected override string ButtonInternalName => "MEPClashPrecheckButton";
    protected override string ButtonText => "MEPClashPrecheck";
    protected override string ToolTip => "Lightweight interference check between MEP disciplines before formal coordination.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
