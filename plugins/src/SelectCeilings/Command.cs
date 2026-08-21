using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.SelectCeilings;

/// <summary>One click sets the active selection to every ceiling visible in the active view — see QuickSelectEngine.</summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "selectceilings";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var count = QuickSelectEngine.SelectCategory(uiDoc, BuiltInCategory.OST_Ceilings);
        if (count == 0)
            TaskDialog.Show("BIMFlow — Ceilings", "No ceilings are visible in the active view.");
        return Result.Succeeded;
    }
}
