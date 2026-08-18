using BIMFlow.Shared;

namespace BIMFlow.SelectExteriorInterior;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Renumbering & Productivity";
    protected override string ButtonInternalName => "SelectExteriorInteriorButton";
    protected override string ButtonText => "SelectExteriorInterior";
    protected override string ToolTip => "Select every exterior or interior wall, door, or window in one click.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
