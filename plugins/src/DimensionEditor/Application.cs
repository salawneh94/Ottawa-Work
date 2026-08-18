using BIMFlow.Shared;

namespace BIMFlow.DimensionEditor;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "DimensionEditorButton";
    protected override string ButtonText => "DimensionEditor";
    protected override string ToolTip => "Edit a dimension's override value, prefix, and suffix directly.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
