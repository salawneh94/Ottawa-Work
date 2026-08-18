using BIMFlow.Shared;

namespace BIMFlow.SelectByCategory;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Renumbering & Productivity";
    protected override string ButtonInternalName => "SelectByCategoryButton";
    protected override string ButtonText => "SelectByCategory";
    protected override string ToolTip => "One-click select every element of a category, in the view or the whole project.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
