using BIMFlow.Shared;

namespace BIMFlow.CrossProjectTransfer;

public class Application : BimFlowApplication
{
    protected override string PanelName => "Data & Coordination";
    protected override string ButtonInternalName => "CrossProjectTransferButton";
    protected override string ButtonText => "CrossProjectTransfer";
    protected override string ToolTip => "Copy element types from one open Revit project into another.";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}
