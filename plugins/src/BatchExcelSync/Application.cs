using OttawaWork.Shared;

namespace OttawaWork.BatchExcelSync;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Data & Coordination";
    protected override string ButtonInternalName => "BatchExcelSyncButton";
    protected override string ButtonText => "Batch Excel Sync";
    protected override string ToolTip => "Export any category's parameters to Excel, edit them, then re-import with a live diff and approve exactly which changes to commit.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
