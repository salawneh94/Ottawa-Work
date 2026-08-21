using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.SelectExtDoors;

/// <summary>One click sets the active selection to every exterior door visible in the active view — see QuickSelectEngine.</summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "selectextdoors";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var count = QuickSelectEngine.SelectDoors(uiDoc, QuickSelectEngine.Side.Exterior);
        if (count == 0)
            TaskDialog.Show("Ottawa Tools — Ext Doors", "No exterior doors are visible in the active view.");
        return Result.Succeeded;
    }
}
