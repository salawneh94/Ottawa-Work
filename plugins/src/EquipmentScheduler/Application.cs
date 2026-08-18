using BIMFlow.Shared;

namespace BIMFlow.EquipmentScheduler;

public class Application : BimFlowApplication
{
    protected override string PanelName => "MEP & Structural";
    protected override string ButtonInternalName => "EquipmentSchedulerButton";
    protected override string ButtonText => "EquipmentScheduler";
    protected override string ToolTip => "Generate equipment schedules linked to cut-sheet data.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
