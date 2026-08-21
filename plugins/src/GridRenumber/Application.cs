using OttawaWork.Shared;

namespace OttawaWork.GridRenumber;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Renumbering & Productivity";
    protected override string ButtonInternalName => "GridRenumberButton";
    protected override string ButtonText => "Grid Renumber";
    protected override string ToolTip => "Batch rename grids or levels without breaking references.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
