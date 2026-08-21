using BIMFlow.Shared;

namespace BIMFlow.SelectWindows;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Select";
    protected override string ButtonInternalName => "SelectWindowsButton";
    protected override string ButtonText => "Windows";
    protected override string ToolTip => "Select every window visible in the active view.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
