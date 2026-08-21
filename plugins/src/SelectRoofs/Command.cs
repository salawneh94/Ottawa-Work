using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.SelectRoofs;

/// <summary>One click sets the active selection to every roof visible in the active view — see QuickSelectEngine.</summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "selectroofs";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var count = QuickSelectEngine.SelectCategory(uiDoc, BuiltInCategory.OST_Roofs);
        if (count == 0)
            TaskDialog.Show("Ottawa Tools — Roofs", "No roofs are visible in the active view.");
        return Result.Succeeded;
    }
}
