using BIMFlow.Shared;

namespace BIMFlow.LegendBuilder;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "LegendBuilderButton";
    protected override string ButtonText => "LegendBuilder";
    protected override string ToolTip => "Auto-build a legend view from every detail component and annotation symbol actually used in the model.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
