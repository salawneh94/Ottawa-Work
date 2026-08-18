using BIMFlow.Shared;

namespace BIMFlow.RoomFinishSchedule;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Rooms";
    protected override string ButtonInternalName => "RoomFinishScheduleButton";
    protected override string ButtonText => "RoomFinishSchedule";
    protected override string ToolTip => "Generate a room finish schedule and export it to CSV.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
