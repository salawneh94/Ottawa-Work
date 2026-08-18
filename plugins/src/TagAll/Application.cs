using BIMFlow.Shared;

namespace BIMFlow.TagAll;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "TagAllButton";
    protected override string ButtonText => "TagAll+";
    protected override string ToolTip => "Smart batch tagging with collision avoidance.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
