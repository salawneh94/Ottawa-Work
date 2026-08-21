using OttawaWork.Shared;

namespace OttawaWork.PointCloudColorizer;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Highlight";
    protected override string ButtonInternalName => "PointCloudColorizerButton";
    protected override string ButtonText => "PointCloudColorizer";
    protected override string ToolTip => "Color-tint point cloud links so multiple scans are visually distinguishable.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
