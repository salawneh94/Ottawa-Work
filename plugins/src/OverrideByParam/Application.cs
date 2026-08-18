using BIMFlow.Shared;

namespace BIMFlow.OverrideByParam;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Highlight";
    protected override string ButtonInternalName => "OverrideByParamButton";
    protected override string ButtonText => "OverrideByParam";
    protected override string ToolTip => "Color-code any category by any parameter value in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
