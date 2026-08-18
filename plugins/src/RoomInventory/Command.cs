using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.RoomInventory;

/// <summary>
/// For every room, finds every element of the chosen categories whose
/// location point falls inside it (via Room.IsPointInRoom) and reports a
/// per-room category breakdown, exported to a branded Excel workbook.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "roominventory";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var rooms = new FilteredElementCollector(doc)
            .OfClass(typeof(SpatialElement))
            .OfType<Room>()
            .Where(r => r.Area > 0)
            .ToList();

        if (rooms.Count == 0)
        {
            TaskDialog.Show("BIMFlow — RoomInventory", "No placed rooms were found in this project.");
            return Result.Succeeded;
        }

        var window = new RoomInventoryWindow();
        if (window.ShowDialog() != true || window.SelectedCategories.Count == 0)
            return Result.Cancelled;

        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .WherePasses(new ElementMulticategoryFilter(window.SelectedCategories))
            .Select(e => (Element: e, Point: (e.Location as LocationPoint)?.Point))
            .Where(t => t.Point is not null)
            .ToList();

        var inventory = rooms.ToDictionary(r => r.Id, _ => new Dictionary<string, int>());

        foreach (var (element, point) in elements)
        {
            var room = rooms.FirstOrDefault(r => r.IsPointInRoom(point!));
            if (room is null) continue;

            var categoryName = element.Category?.Name ?? "Unknown";
            var counts = inventory[room.Id];
            counts[categoryName] = counts.GetValueOrDefault(categoryName) + 1;
        }

        var allCategories = inventory.Values.SelectMany(v => v.Keys).Distinct().OrderBy(c => c).ToList();

        var rows = rooms.OrderBy(r => r.Number).Select(room =>
        {
            var counts = inventory[room.Id];
            var values = new List<string> { room.Number, room.Name };
            values.AddRange(allCategories.Select(c => counts.GetValueOrDefault(c).ToString()));
            return values;
        }).ToList();

        var path = BrandedXlsx.Save(
            "Export room inventory",
            "room-inventory.xlsx",
            "Room Inventory",
            $"{doc.Title} — element count per room",
            new[] { "Room Number", "Room Name" }.Concat(allCategories).ToList(),
            rows);
        if (path is null) return Result.Cancelled;

        TaskDialog.Show("BIMFlow — RoomInventory", $"Exported inventory for {rooms.Count} room(s) to:\n{path}");
        return Result.Succeeded;
    }
}
