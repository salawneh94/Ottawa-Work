using BIMFlow.Shared;

namespace BIMFlow.ParameterMapper;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Parameters & Families";
    protected override string ButtonInternalName => "ParameterMapperButton";
    protected override string ButtonText => "ParameterMapper";
    protected override string ToolTip => "Transfer parameter values between related elements by rule.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
