using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.SelectExtWalls;

/// <summary>One click sets the active selection to every exterior wall visible in the active view — see QuickSelectEngine.</summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "selectextwalls";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var count = QuickSelectEngine.SelectWalls(uiDoc, QuickSelectEngine.Side.Exterior);
        if (count == 0)
            TaskDialog.Show("Ottawa Tools — Ext Walls", "No exterior walls are visible in the active view.");
        return Result.Succeeded;
    }
}
