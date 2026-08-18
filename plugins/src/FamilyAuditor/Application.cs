using BIMFlow.Shared;

namespace BIMFlow.FamilyAuditor;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Parameters & Families";
    protected override string ButtonInternalName => "FamilyAuditorButton";
    protected override string ButtonText => "FamilyAuditor";
    protected override string ToolTip => "Scan families for best-practice issues before they ship.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
