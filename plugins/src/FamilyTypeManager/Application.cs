using BIMFlow.Shared;

namespace BIMFlow.FamilyTypeManager;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Parameters & Families";
    protected override string ButtonInternalName => "FamilyTypeManagerButton";
    protected override string ButtonText => "FamilyTypeManager";
    protected override string ToolTip => "Bulk rename, reorganize, and purge unused family types.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
