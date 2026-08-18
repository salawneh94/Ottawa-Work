using BIMFlow.Shared;

namespace BIMFlow.ParameterFormulaPropagator;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Parameters & Families";
    protected override string ButtonInternalName => "ParameterFormulaPropagatorButton";
    protected override string ButtonText => "ParameterFormulaPropagator";
    protected override string ToolTip => "Generate parameter values from a template pattern and sequential numbering.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
