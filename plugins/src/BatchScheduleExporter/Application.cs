using BIMFlow.Shared;

namespace BIMFlow.BatchScheduleExporter;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Data & Coordination";
    protected override string ButtonInternalName => "BatchScheduleExporterButton";
    protected override string ButtonText => "BatchScheduleExporter";
    protected override string ToolTip => "Export every schedule in the project to individual CSV files in one pass.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
