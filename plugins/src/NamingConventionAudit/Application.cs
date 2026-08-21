using OttawaWork.Shared;

namespace OttawaWork.NamingConventionAudit;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "NamingConventionAuditButton";
    protected override string ButtonText => "NamingConventionAudit";
    protected override string ToolTip => "Check view, sheet, or family type names against a regex pattern you define.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
