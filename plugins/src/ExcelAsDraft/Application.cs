using BIMFlow.Shared;

namespace BIMFlow.ExcelAsDraft;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Data & Coordination";
    protected override string ButtonInternalName => "ExcelAsDraftButton";
    protected override string ButtonText => "ExcelAsDraft";
    protected override string ToolTip => "Import a CSV as a visual grid table in a drafting view, with one-click refresh.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
