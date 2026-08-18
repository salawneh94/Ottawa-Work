using BIMFlow.Shared;

namespace BIMFlow.RevisionTrack;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "RevisionTrackButton";
    protected override string ButtonText => "RevisionTrack";
    protected override string ToolTip => "Automate revision clouds and revision schedules across sheet sets.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
