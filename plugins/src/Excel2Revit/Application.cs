using BIMFlow.Shared;

namespace BIMFlow.Excel2Revit;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Data & Coordination";
    protected override string ButtonInternalName => "Excel2RevitButton";
    protected override string ButtonText => "Excel2Revit";
    protected override string ToolTip => "Two-way sync between Excel/CSV and Revit parameters or schedules.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
