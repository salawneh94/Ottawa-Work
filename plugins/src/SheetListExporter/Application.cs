using BIMFlow.Shared;

namespace BIMFlow.SheetListExporter;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "SheetListExporterButton";
    protected override string ButtonText => "Sheet List";
    protected override string ToolTip => "Export or re-import the sheet list to/from CSV.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
