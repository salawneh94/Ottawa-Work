using BIMFlow.Shared;

namespace BIMFlow.SelectCeilings;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectCeilingsButton";
    protected override string ButtonText => "Ceilings";
    protected override string ToolTip => "Select every ceiling visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
