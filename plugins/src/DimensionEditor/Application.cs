using OttawaWork.Shared;

namespace OttawaWork.DimensionEditor;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "DimensionEditorButton";
    protected override string ButtonText => "DimensionEditor";
    protected override string ToolTip => "Edit a dimension's override value, prefix, and suffix directly.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
