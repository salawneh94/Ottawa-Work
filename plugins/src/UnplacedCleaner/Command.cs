using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.UnplacedCleaner;

/// <summary>
/// Finds Room elements that were created (via a schedule row, a copy/paste,
/// an import) but never actually placed on a plan — Room.Location is null —
/// and lets the user review and delete them in one pass. Distinct from
/// RoomFinisher, which reports unplaced/unenclosed/unbounded rooms without
/// offering to delete anything.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "unplacedcleaner";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var unplacedRooms = new FilteredElementCollector(doc)
            .OfClass(typeof(SpatialElement))
            .OfType<Room>()
            .Where(r => r.Location is null)
            .ToList();

        if (unplacedRooms.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — Unplaced Cleaner", "No unplaced rooms were found in this project.");
            return Result.Succeeded;
        }

        var window = new UnplacedCleanerWindow(unplacedRooms);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        var toDelete = window.SelectedElementIdsToDelete;
        if (toDelete.Count == 0)
            return Result.Succeeded;

        using var transaction = new Transaction(doc, "Ottawa Tools: Delete Unplaced Rooms");
        transaction.Start();
        try
        {
            doc.Delete(toDelete);
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("Ottawa Tools — Unplaced Cleaner", $"Deleted {toDelete.Count} unplaced room(s).");
        return Result.Succeeded;
    }
}
