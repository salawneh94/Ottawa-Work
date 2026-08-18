using BIMFlow.Shared;

namespace BIMFlow.CropSync;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Sheets & Views";
    protected override string ButtonInternalName => "CropSyncButton";
    protected override string ButtonText => "Crop Sync";
    protected override string ToolTip => "Copy the crop region and view range from the active view to other views.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
