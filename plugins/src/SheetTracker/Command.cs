using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.SheetTracker;

/// <summary>
/// Opens Sheet Tracker. Read-only scan, so no transaction; the only follow-up action is switching the
/// active view to a chosen sheet, which happens here after the modal window has closed rather than
/// from inside it.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "sheettracker";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new SheetTrackerWindow(doc);
        if (window.ShowDialog() != true || window.SheetToOpen is null)
            return Result.Succeeded;

        if (doc.GetElement(window.SheetToOpen) is ViewSheet sheet)
            uiDoc.ActiveView = sheet;

        return Result.Succeeded;
    }
}
