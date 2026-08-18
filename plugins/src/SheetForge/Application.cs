using BIMFlow.Shared;

namespace BIMFlow.SheetForge;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "SheetForgeButton";
    protected override string ButtonText => "SheetForge";
    protected override string ToolTip => "Batch-create sheets from an Excel list in one pass.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
