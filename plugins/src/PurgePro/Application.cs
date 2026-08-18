using BIMFlow.Shared;

namespace BIMFlow.PurgePro;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "PurgeProButton";
    protected override string ButtonText => "Purge Pro";
    protected override string ToolTip => "Repeat Revit's purge-unused until the model stops shrinking.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
