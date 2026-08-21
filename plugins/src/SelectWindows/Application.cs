using OttawaWork.Shared;

namespace OttawaWork.SelectWindows;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectWindowsButton";
    protected override string ButtonText => "Windows";
    protected override string ToolTip => "Select every window visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
