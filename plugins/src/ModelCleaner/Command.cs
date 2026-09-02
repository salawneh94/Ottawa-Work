using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.ModelCleaner;

/// <summary>
/// Deep model audit across seven categories (ModelCleanerEngine): in-place
/// families, unplaced views, unused view templates/filters, duplicate text
/// note types, broken RVT links, orphaned materials, and blank sheets/
/// unplaced schedules. Select/Delete act on whatever's checked (or every
/// finding shown if nothing's checked, same convention as every other
/// batch tool in this codebase); Show Blast Radius is read-only and
/// answered directly inside the window, no transaction needed.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "modelcleaner";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new ModelCleanerWindow(doc);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.Action == ModelCleanerAction.Select)
        {
            uiDoc.Selection.SetElementIds(window.TargetElementIds);
            return Result.Succeeded;
        }

        if (window.Action == ModelCleanerAction.Delete)
        {
            using var transaction = new Transaction(doc, "Ottawa Tools: Model Cleaner Delete");
            transaction.Start();
            try
            {
                var deleted = doc.Delete(window.TargetElementIds);
                transaction.Commit();
                TaskDialog.Show("Ottawa Tools — Model Cleaner", $"Deleted {deleted.Count} element(s) (including any dependents Revit removed along with them).");
            }
            catch (Exception ex)
            {
                transaction.RollBack();
                TaskDialog.Show("Ottawa Tools — Model Cleaner", $"Couldn't delete: {ex.Message}");
                return Result.Failed;
            }
        }

        return Result.Succeeded;
    }
}
