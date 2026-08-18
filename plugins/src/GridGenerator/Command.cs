using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;

namespace BIMFlow.GridGenerator;

/// <summary>
/// Generates straight structural grids from a CSV
/// (Name, Direction[Vertical|Horizontal], Position, Start, End — all in
/// meters). Parsing grid positions out of an imported CAD file is a much
/// larger, less certain undertaking (DWG layer conventions vary a lot
/// between offices); a CSV of positions is the part every office can
/// produce reliably from their own standard.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "gridgenerator";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        using var dialog = new OpenFileDialog
        {
            Title = "Select grid list CSV (columns: Name, Direction, Position, Start, End — meters)",
            Filter = "CSV files (*.csv)|*.csv",
        };
        if (dialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

        var lines = File.ReadAllLines(dialog.FileName);
        if (lines.Length < 2)
        {
            TaskDialog.Show("BIMFlow — GridGenerator", "That file has no data rows.");
            return Result.Cancelled;
        }

        var header = Csv.ParseLine(lines[0]);
        var nameIdx = header.IndexOf("Name");
        var dirIdx = header.IndexOf("Direction");
        var posIdx = header.IndexOf("Position");
        var startIdx = header.IndexOf("Start");
        var endIdx = header.IndexOf("End");

        if (new[] { nameIdx, dirIdx, posIdx, startIdx, endIdx }.Any(i => i < 0))
        {
            TaskDialog.Show("BIMFlow — GridGenerator", "The CSV needs Name, Direction, Position, Start, End columns.");
            return Result.Cancelled;
        }

        var created = 0;
        var skipped = new List<string>();

        using var transaction = new Transaction(doc, "BIMFlow: Generate Grids");
        transaction.Start();
        try
        {
            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var fields = Csv.ParseLine(lines[i]);

                var name = fields.ElementAtOrDefault(nameIdx) ?? "";
                var direction = fields.ElementAtOrDefault(dirIdx)?.Trim().ToLowerInvariant() ?? "";
                var hasPos = double.TryParse(fields.ElementAtOrDefault(posIdx), out var positionMeters);
                var hasStart = double.TryParse(fields.ElementAtOrDefault(startIdx), out var startMeters);
                var hasEnd = double.TryParse(fields.ElementAtOrDefault(endIdx), out var endMeters);

                if (!hasPos || !hasStart || !hasEnd || string.IsNullOrWhiteSpace(name))
                {
                    skipped.Add($"Row {i + 1}: missing or invalid data");
                    continue;
                }

                var position = UnitUtils.ConvertToInternalUnits(positionMeters, UnitTypeId.Meters);
                var start = UnitUtils.ConvertToInternalUnits(startMeters, UnitTypeId.Meters);
                var end = UnitUtils.ConvertToInternalUnits(endMeters, UnitTypeId.Meters);

                Line line;
                if (direction.StartsWith("v"))
                    line = Line.CreateBound(new XYZ(position, start, 0), new XYZ(position, end, 0));
                else if (direction.StartsWith("h"))
                    line = Line.CreateBound(new XYZ(start, position, 0), new XYZ(end, position, 0));
                else
                {
                    skipped.Add($"Row {i + 1}: Direction must be Vertical or Horizontal");
                    continue;
                }

                try
                {
                    var grid = Grid.Create(doc, line);
                    grid.Name = name;
                    created++;
                }
                catch (Exception)
                {
                    skipped.Add($"Row {i + 1} ({name}): couldn't create grid — name may already be in use");
                }
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        var summary = $"Created {created} grid(s).";
        if (skipped.Count > 0)
            summary += $"\n\nSkipped {skipped.Count}:\n" + string.Join('\n', skipped.Take(10));

        TaskDialog.Show("BIMFlow — GridGenerator", summary);
        return Result.Succeeded;
    }
}
