using BIMFlow.Shared;

namespace BIMFlow.BatchExporter;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Renumbering & Productivity";
    protected override string ButtonInternalName => "BatchExporterButton";
    protected override string ButtonText => "Batch Exporter";
    protected override string ToolTip => "Export sheets or views to PDF, DWG, or NWC with a naming template.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
