using BIMFlow.Shared;

namespace BIMFlow.TitleBlockUpdater;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "TitleBlockUpdaterButton";
    protected override string ButtonText => "TitleBlockUpdater";
    protected override string ToolTip => "Batch-update title block info across sheets and linked projects.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
