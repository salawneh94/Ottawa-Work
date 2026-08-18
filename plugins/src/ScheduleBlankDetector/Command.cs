using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.ScheduleBlankDetector;

/// <summary>
/// Reads a schedule's actual rendered table data (same content you'd see
/// on the sheet) and reports which cells are blank, plus an overall
/// completeness score. Reading the rendered table rather than re-querying
/// elements directly means it automatically respects whatever filtering,
/// sorting, and grouping the schedule already has.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "scheduleblankdetector";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var schedules = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Where(s => !s.IsTemplate)
            .OrderBy(s => s.Name)
            .ToList();

        if (schedules.Count == 0)
        {
            TaskDialog.Show("BIMFlow — ScheduleBlankDetector", "No schedules were found in this project.");
            return Result.Succeeded;
        }

        var picker = new SimplePickerDialog("BIMFlow — ScheduleBlankDetector", "Schedule to audit:", schedules.Select(s => s.Name).ToList());
        if (picker.ShowDialog() != true || picker.SelectedText is null)
            return Result.Cancelled;

        var schedule = schedules.First(s => s.Name == picker.SelectedText);

        TableData tableData;
        TableSectionData body;
        try
        {
            tableData = schedule.GetTableData();
            body = tableData.GetSectionData(SectionType.Body);
        }
        catch (Exception)
        {
            TaskDialog.Show("BIMFlow — ScheduleBlankDetector", "Couldn't read this schedule's table data.");
            return Result.Cancelled;
        }

        var totalCells = 0;
        var blankCells = 0;
        var blankRows = new List<int>();

        for (var row = 0; row < body.NumberOfRows; row++)
        {
            var rowHasBlank = false;
            for (var col = 0; col < body.NumberOfColumns; col++)
            {
                totalCells++;
                var text = schedule.GetCellText(SectionType.Body, row, col);
                if (string.IsNullOrWhiteSpace(text))
                {
                    blankCells++;
                    rowHasBlank = true;
                }
            }
            if (rowHasBlank) blankRows.Add(row);
        }

        if (totalCells == 0)
        {
            TaskDialog.Show("BIMFlow — ScheduleBlankDetector", "That schedule has no rows to check.");
            return Result.Succeeded;
        }

        var completeness = 100.0 * (totalCells - blankCells) / totalCells;

        TaskDialog.Show(
            "BIMFlow — ScheduleBlankDetector",
            $"\"{schedule.Name}\": {completeness:F0}% complete.\n\n" +
            $"{blankCells} of {totalCells} cell(s) are blank, across {blankRows.Count} of {body.NumberOfRows} row(s).");

        return Result.Succeeded;
    }
}
