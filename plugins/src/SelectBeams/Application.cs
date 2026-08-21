using OttawaWork.Shared;

namespace OttawaWork.SelectBeams;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectBeamsButton";
    protected override string ButtonText => "Beams";
    protected override string ToolTip => "Select every beam visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
