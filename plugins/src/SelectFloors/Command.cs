using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.SelectFloors;

/// <summary>One click sets the active selection to every floor visible in the active view — see QuickSelectEngine.</summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "selectfloors";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var count = QuickSelectEngine.SelectCategory(uiDoc, BuiltInCategory.OST_Floors);
        if (count == 0)
            TaskDialog.Show("BIMFlow — Floors", "No floors are visible in the active view.");
        return Result.Succeeded;
    }
}
