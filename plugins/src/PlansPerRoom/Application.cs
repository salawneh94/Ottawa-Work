using OttawaWork.Shared;

namespace OttawaWork.PlansPerRoom;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Rooms";
    protected override string ButtonInternalName => "PlansPerRoomButton";
    protected override string ButtonText => "Plans Per Room";
    protected override string ToolTip => "Build a full room-data sheet set with editable naming templates and per-room finish parameters.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
