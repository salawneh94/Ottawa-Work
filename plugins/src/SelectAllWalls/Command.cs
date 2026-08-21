using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.SelectAllWalls;

/// <summary>One click sets the active selection to every wall visible in the active view — see QuickSelectEngine.</summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "selectallwalls";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var count = QuickSelectEngine.SelectWalls(uiDoc, QuickSelectEngine.Side.Any);
        if (count == 0)
            TaskDialog.Show("BIMFlow — All Walls", "No walls are visible in the active view.");
        return Result.Succeeded;
    }
}
