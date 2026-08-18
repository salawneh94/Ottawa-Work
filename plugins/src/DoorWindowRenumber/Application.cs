using BIMFlow.Shared;

namespace BIMFlow.DoorWindowRenumber;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Renumbering & Productivity";
    protected override string ButtonInternalName => "DoorWindowRenumberButton";
    protected override string ButtonText => "Door/Window Renumber";
    protected override string ToolTip => "Renumber doors or windows with a configurable scheme.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
