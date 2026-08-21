using BIMFlow.Shared;

namespace BIMFlow.ParamPowerSuite;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Parameters";
    protected override string ButtonInternalName => "ParamPowerSuiteButton";
    protected override string ButtonText => "Param Power Suite";
    protected override string ToolTip => "Bulk-edit parameters across every loaded element: set, find/replace, transform, copy, combine, jam to shared, or create a new bound parameter — all in one tabbed workbench.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
