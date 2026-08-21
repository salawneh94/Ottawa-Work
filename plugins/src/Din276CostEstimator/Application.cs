using OttawaWork.Shared;

namespace OttawaWork.Din276CostEstimator;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Parameters";
    protected override string ButtonInternalName => "Din276CostEstimatorButton";
    protected override string ButtonText => "DIN 276 Costs";
    protected override string ToolTip => "Classify elements into DIN 276 Kostengruppen and price them against your own unit rates — live quantities, editable rates, exportable report.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
