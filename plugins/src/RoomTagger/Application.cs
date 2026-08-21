using OttawaWork.Shared;

namespace OttawaWork.RoomTagger;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Rooms";
    protected override string ButtonInternalName => "RoomTaggerButton";
    protected override string ButtonText => "RoomTagger";
    protected override string ToolTip => "Write each room's number/name onto every element found inside it.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
