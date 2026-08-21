using OttawaWork.Shared;

namespace OttawaWork.RoomRenumber;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Renumbering & Productivity";
    protected override string ButtonInternalName => "RoomRenumberButton";
    protected override string ButtonText => "Room Renumber";
    protected override string ToolTip => "Renumber rooms by direction of travel or zone.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
