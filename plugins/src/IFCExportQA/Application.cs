using OttawaWork.Shared;

namespace OttawaWork.IFCExportQA;

public class Application : OttawaWorkApplication
{
    protected override string PanelName => "Data & Coordination";
    protected override string ButtonInternalName => "IFCExportQAButton";
    protected override string ButtonText => "IFCExportQA";
    protected override string ToolTip => "Pre-flight checks and mapping presets for clean IFC exports.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
