using BIMFlow.Shared;

namespace BIMFlow.ViewRenamer;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Renumbering & Productivity";
    protected override string ButtonInternalName => "ViewRenamerButton";
    protected override string ButtonText => "View Renamer";
    protected override string ToolTip => "Batch rename views with find/replace, prefix/suffix, or regex.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
