using BIMFlow.Shared;

namespace BIMFlow.GridRenumber;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Renumbering & Productivity";
    protected override string ButtonInternalName => "GridRenumberButton";
    protected override string ButtonText => "Grid Renumber";
    protected override string ToolTip => "Batch rename grids or levels without breaking references.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
