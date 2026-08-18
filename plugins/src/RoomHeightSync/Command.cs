using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;

namespace BIMFlow.RoomHeightSync;

/// <summary>
/// Reads a CSV of RoomNumber,HeightMeters and writes each row's height to
/// that room's Limit Offset (BuiltInParameter.ROOM_UPPER_OFFSET) — the
/// parameter that controls how tall Revit considers the room for area and
/// volume computation.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "roomheightsync";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        using var openDialog = new OpenFileDialog
        {
            Title = "Import room heights (columns: RoomNumber, HeightMeters)",
            Filter = "CSV files (*.csv)|*.csv",
        };
        if (openDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

        var lines = File.ReadAllLines(openDialog.FileName);
        if (lines.Length < 2)
        {
            TaskDialog.Show("BIMFlow — RoomHeightSync", "That file has no data rows.");
            return Result.Cancelled;
        }

        var header = Csv.ParseLine(lines[0]);
        var numberIndex = header.IndexOf("RoomNumber");
        var heightIndex = header.IndexOf("HeightMeters");
        if (numberIndex < 0 || heightIndex < 0)
        {
            TaskDialog.Show("BIMFlow — RoomHeightSync", "The CSV needs RoomNumber and HeightMeters columns.");
            return Result.Cancelled;
        }

        var roomsByNumber = new FilteredElementCollector(doc)
            .OfClass(typeof(SpatialElement))
            .OfType<Room>()
            .Where(r => r.Area > 0)
            .GroupBy(r => r.Number)
            .ToDictionary(g => g.Key, g => g.First());

        var updated = 0;
        var unmatched = new List<string>();

        using var transaction = new Transaction(doc, "BIMFlow: Sync Room Heights");
        transaction.Start();
        try
        {
            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var fields = Csv.ParseLine(lines[i]);
                if (fields.Count <= Math.Max(numberIndex, heightIndex)) continue;

                var roomNumber = fields[numberIndex];
                if (!roomsByNumber.TryGetValue(roomNumber, out var room))
                {
                    unmatched.Add(roomNumber);
                    continue;
                }

                if (!double.TryParse(fields[heightIndex], out var heightMeters)) continue;

                var param = room.get_Parameter(BuiltInParameter.ROOM_UPPER_OFFSET);
                if (param is null || param.IsReadOnly) continue;

                param.Set(UnitUtils.ConvertToInternalUnits(heightMeters, UnitTypeId.Meters));
                updated++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        var summary = $"Updated {updated} room(s).";
        if (unmatched.Count > 0)
            summary += $"\n\n{unmatched.Count} row(s) didn't match a room by number:\n{string.Join(", ", unmatched.Take(20))}";

        TaskDialog.Show("BIMFlow — RoomHeightSync", summary);
        return Result.Succeeded;
    }
}
