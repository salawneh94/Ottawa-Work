using BIMFlow.Shared;

namespace BIMFlow.ScheduleBlankDetector;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "ScheduleBlankDetectorButton";
    protected override string ButtonText => "ScheduleBlankDetector";
    protected override string ToolTip => "Find blank fields in a schedule and score how complete it is.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
