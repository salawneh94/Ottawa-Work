using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;

namespace BIMFlow.BatchScheduleExporter;

/// <summary>
/// Exports every schedule view in the project to its own branded Excel
/// workbook via Revit's native schedule export (re-formatted from the CSV
/// Revit writes, since Revit controls the column list) — the batch version
/// of the single-schedule export Excel2Revit already does. Also goes
/// through ScheduleExportHelper so every export has an Element ID column
/// even if the schedule wasn't set up with one, so Excel2Revit's import
/// mode has something to match edited rows back to elements on later.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "batchscheduleexporter";

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
            TaskDialog.Show("BIMFlow — BatchScheduleExporter", "No schedules were found in this project.");
            return Result.Succeeded;
        }

        using var folderDialog = new FolderBrowserDialog { Description = "Choose a folder for the exported Excel files" };
        if (folderDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

        var options = new ViewScheduleExportOptions
        {
            FieldDelimiter = ",",
            TextQualifier = ExportTextQualifier.DoubleQuote,
            ColumnHeaders = ExportColumnHeaders.OneRow,
            Title = false,
            HeadersFootersBlanks = false,
        };

        var exported = 0;
        var failed = new List<string>();
        var usedNames = new HashSet<string>();

        foreach (var schedule in schedules)
        {
            var fileName = SanitizeFileName(schedule.Name);
            var uniqueName = fileName;
            var suffix = 2;
            while (!usedNames.Add(uniqueName))
                uniqueName = $"{fileName}_{suffix++}";

            try
            {
                var csvPath = Path.Combine(folderDialog.SelectedPath, uniqueName + ".csv");
                ScheduleExportHelper.ExportWithElementId(doc, schedule, folderDialog.SelectedPath, uniqueName + ".csv", options);
                BrandedXlsx.ReplaceCsvWithBrandedXlsx(csvPath, schedule.Name, doc.Title);
                exported++;
            }
            catch (Exception)
            {
                failed.Add(schedule.Name);
            }
        }

        var summary = $"Exported {exported} of {schedules.Count} schedule(s) to:\n{folderDialog.SelectedPath}";
        if (failed.Count > 0)
            summary += $"\n\nCouldn't export: {string.Join(", ", failed)}";

        TaskDialog.Show("BIMFlow — BatchScheduleExporter", summary);
        return Result.Succeeded;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length == 0 ? "Schedule" : sanitized;
    }
}
