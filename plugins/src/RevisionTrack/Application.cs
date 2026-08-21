using OttawaWork.Shared;

namespace OttawaWork.RevisionTrack;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "RevisionTrackButton";
    protected override string ButtonText => "RevisionTrack";
    protected override string ToolTip => "Automate revision clouds and revision schedules across sheet sets.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
