using OttawaWork.Shared;

namespace OttawaWork.HighlightInterior;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Highlight";
    protected override string ButtonInternalName => "HighlightInteriorButton";
    protected override string ButtonText => "HL Interior";
    protected override string ToolTip => "Toggle a blue color highlight on every interior wall in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
