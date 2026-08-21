using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.SelectAllWalls;

/// <summary>One click sets the active selection to every wall visible in the active view — see QuickSelectEngine.</summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "selectallwalls";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var count = QuickSelectEngine.SelectWalls(uiDoc, QuickSelectEngine.Side.Any);
        if (count == 0)
            TaskDialog.Show("Ottawa Tools — All Walls", "No walls are visible in the active view.");
        return Result.Succeeded;
    }
}
