using BIMFlow.Shared;

namespace BIMFlow.ViewSync;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "ViewSyncButton";
    protected override string ButtonText => "ViewSync";
    protected override string ToolTip => "Reassign view templates across hundreds of views at once.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
