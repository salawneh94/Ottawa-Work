using BIMFlow.Shared;

namespace BIMFlow.SharedCoordinatesAudit;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Data & Coordination";
    protected override string ButtonInternalName => "SharedCoordinatesAuditButton";
    protected override string ButtonText => "SharedCoordinatesAudit";
    protected override string ToolTip => "Check whether every linked model's survey point actually lines up with this project's shared coordinates.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
