using OttawaWork.Shared;

namespace OttawaWork.StairCalculator;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Model QA/QC & Cleanup";
    protected override string ButtonInternalName => "StairCalculatorButton";
    protected override string ButtonText => "StairCalculator";
    protected override string ToolTip => "Check stair riser/tread proportions against the 2R+G comfort rule.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
