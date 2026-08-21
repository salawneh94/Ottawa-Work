using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.SelectIntWalls;

/// <summary>One click sets the active selection to every interior wall visible in the active view — see QuickSelectEngine.</summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "selectintwalls";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var count = QuickSelectEngine.SelectWalls(uiDoc, QuickSelectEngine.Side.Interior);
        if (count == 0)
            TaskDialog.Show("Ottawa Tools — Int Walls", "No interior walls are visible in the active view.");
        return Result.Succeeded;
    }
}
