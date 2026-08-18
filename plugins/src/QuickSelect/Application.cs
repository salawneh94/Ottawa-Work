using BIMFlow.Shared;

namespace BIMFlow.QuickSelect;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Renumbering & Productivity";
    protected override string ButtonInternalName => "QuickSelectButton";
    protected override string ButtonText => "QuickSelect+";
    protected override string ToolTip => "Select elements by category and parameter-value rules.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
