using OttawaWork.Shared;

namespace OttawaWork.PointCloudHeatmap;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Highlight";
    protected override string ButtonInternalName => "PointCloudHeatmapButton";
    protected override string ButtonText => "PC Heatmap";
    protected override string ToolTip => "Compare walls against point cloud scan data and color-code deviations.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
