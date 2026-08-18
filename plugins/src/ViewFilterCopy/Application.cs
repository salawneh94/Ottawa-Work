using BIMFlow.Shared;

namespace BIMFlow.ViewFilterCopy;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "ViewFilterCopyButton";
    protected override string ButtonText => "Copy Filters";
    protected override string ToolTip => "Copy view filters and overrides from the active view to other views.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
