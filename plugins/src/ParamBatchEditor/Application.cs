using BIMFlow.Shared;

namespace BIMFlow.ParamBatchEditor;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Parameters & Families";
    protected override string ButtonInternalName => "ParamBatchEditorButton";
    protected override string ButtonText => "ParamBatchEditor";
    protected override string ToolTip => "Bulk-edit parameter values in a spreadsheet-style grid.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
