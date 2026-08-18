using BIMFlow.Shared;

namespace BIMFlow.ModelCeilings;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Rooms";
    protected override string ButtonInternalName => "ModelCeilingsButton";
    protected override string ButtonText => "ModelCeilings";
    protected override string ToolTip => "Auto-create drop ceilings from room boundaries in the active floor plan.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
