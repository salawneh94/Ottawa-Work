using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.RoomFinisher;

/// <summary>
/// Scans every level for rooms that are unplaced (no location) or
/// unenclosed (placed but zero area, meaning the boundary isn't closed),
/// and lists them with a jump-to-room action.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "roomfinisher";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var rooms = new FilteredElementCollector(doc)
            .OfClass(typeof(SpatialElement))
            .OfType<Room>()
            .ToList();

        if (rooms.Count == 0)
        {
            TaskDialog.Show("BIMFlow — RoomFinisher", "No rooms were found in this project.");
            return Result.Succeeded;
        }

        var rows = new List<ResultRow>();

        foreach (var room in rooms)
        {
            var levelName = room.Level?.Name ?? "(no level)";
            var name = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? room.Name;
            var number = room.Number;

            if (room.Location is null)
            {
                rows.Add(new ResultRow(new[] { number, name, levelName, "Unplaced" }, new List<ElementId> { room.Id }));
            }
            else if (room.Area <= 0)
            {
                rows.Add(new ResultRow(new[] { number, name, levelName, "Unenclosed (zero area)" }, new List<ElementId> { room.Id }));
            }
        }

        if (rows.Count == 0)
        {
            TaskDialog.Show("BIMFlow — RoomFinisher", $"All {rooms.Count} room(s) are placed and enclosed.");
            return Result.Succeeded;
        }

        var results = new ResultsListForm(
            "BIMFlow — RoomFinisher Results",
            $"{rows.Count} of {rooms.Count} room(s) need attention.",
            new[] { "Number", "Name", "Level", "Issue" },
            rows);

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }
}
