using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.RoomRenumber;

[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "roomrenumber";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var selectedRooms = uiDoc.Selection.GetElementIds()
            .Select(id => doc.GetElement(id))
            .OfType<Room>()
            .ToList();

        var rooms = selectedRooms.Count > 0
            ? selectedRooms
            : new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))
                .OfType<Room>()
                .Where(r => r.Area > 0)
                .ToList();

        if (rooms.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — Room Renumber", "No placed rooms found in the current selection or model.");
            return Result.Succeeded;
        }

        var window = new RoomRenumberWindow(rooms.Count);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        var ordered = OrderRooms(rooms, window.Direction);

        var renames = new List<(Action<string> setValue, string newValue)>();
        var number = window.StartNumber;
        foreach (var room in ordered)
        {
            renames.Add((value => room.Number = value, $"{window.Prefix}{number}"));
            number += window.Increment;
        }

        using var transaction = new Transaction(doc, "Ottawa Tools: Renumber Rooms");
        transaction.Start();
        try
        {
            TwoPassRenamer.Apply(renames);
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("Ottawa Tools — Room Renumber", $"Renumbered {renames.Count} room(s).");
        return Result.Succeeded;
    }

    private static List<Room> OrderRooms(List<Room> rooms, SortDirection direction)
    {
        List<(Room Room, XYZ Point)> located = rooms
            .Select(r => (Room: r, Point: (r.Location as LocationPoint)?.Point))
            .Where(t => t.Point is not null)
            .Select(t => (t.Room, Point: t.Point!))
            .ToList();

        // LeftToRight: traverse row-by-row (top row first, left to right within the row).
        // TopToBottom: traverse column-by-column (leftmost column first, top to bottom within it).
        IOrderedEnumerable<(Room Room, XYZ Point)> ordered = direction == SortDirection.LeftToRightThenTopToBottom
            ? located.OrderBy(t => t.Room.LevelId.Value)
                     .ThenByDescending(t => Math.Round(t.Point.Y, 1))
                     .ThenBy(t => Math.Round(t.Point.X, 1))
            : located.OrderBy(t => t.Room.LevelId.Value)
                     .ThenBy(t => Math.Round(t.Point.X, 1))
                     .ThenByDescending(t => Math.Round(t.Point.Y, 1));

        return ordered.Select(t => t.Room).ToList();
    }
}
