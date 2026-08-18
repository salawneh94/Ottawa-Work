using BIMFlow.Shared;

namespace BIMFlow.LegendUsageAudit;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "LegendUsageAuditButton";
    protected override string ButtonText => "LegendUsageAudit";
    protected override string ToolTip => "Audit which line styles, fill patterns, and detail components are actually used — and how often.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
