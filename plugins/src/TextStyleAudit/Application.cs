using BIMFlow.Shared;

namespace BIMFlow.TextStyleAudit;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "TextStyleAuditButton";
    protected override string ButtonText => "Text Style Audit";
    protected override string ToolTip => "Scan the model for text and dimension styles outside the approved list.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
