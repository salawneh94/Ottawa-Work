using OttawaWork.Shared;

namespace OttawaWork.LegendPlacer;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "LegendPlacerButton";
    protected override string ButtonText => "LegendPlacer";
    protected override string ToolTip => "Place a legend on multiple sheets at a consistent position.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
