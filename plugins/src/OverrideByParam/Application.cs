using BIMFlow.Shared;

namespace BIMFlow.OverrideByParam;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Highlight";
    protected override string ButtonInternalName => "OverrideByParamButton";
    protected override string ButtonText => "Color Code";
    protected override string ToolTip => "Color-code any category by any parameter value — preview live, then apply as real persistent view filters.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
