using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;

namespace BIMFlow.RoomFinishSchedule;

/// <summary>
/// Creates a Room schedule with the standard finish fields (floor, wall,
/// ceiling, base finish, plus number/name/area) and exports it straight to
/// a branded Excel workbook — a one-click finish area takeoff starting point.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "roomfinishschedule";

    private static readonly string[] PreferredFieldNames =
    {
        "Number", "Name", "Area", "Base Finish", "Floor Finish", "Wall Finish", "Ceiling Finish",
    };

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var categoryId = new ElementId(BuiltInCategory.OST_Rooms);

        ViewSchedule schedule;
        try
        {
            schedule = ViewSchedule.CreateSchedule(doc, categoryId);
        }
        catch (Exception)
        {
            TaskDialog.Show("BIMFlow — RoomFinishSchedule", "Couldn't create a Room schedule in this project.");
            return Result.Cancelled;
        }

        using var transaction = new Transaction(doc, "BIMFlow: Create Room Finish Schedule");
        transaction.Start();
        try
        {
            schedule.Name = MakeUniqueName(doc, "Room Finish Schedule");

            var definition = schedule.Definition;
            var schedulable = definition.GetSchedulableFields();

            foreach (var fieldName in PreferredFieldNames)
            {
                var match = schedulable.FirstOrDefault(f => f.GetName(doc).Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                if (match is not null) definition.AddField(match);
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        var exportNow = TaskDialog.Show(
            "BIMFlow — RoomFinishSchedule",
            $"Created \"{schedule.Name}\". Export it to Excel now?",
            TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

        if (exportNow == TaskDialogResult.Yes)
        {
            using var saveDialog = new SaveFileDialog
            {
                Title = "Export room finish schedule",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"{schedule.Name}.xlsx",
            };
            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                var folder = Path.GetDirectoryName(saveDialog.FileName)!;
                var fileNameOnly = Path.GetFileNameWithoutExtension(saveDialog.FileName);
                var csvPath = Path.Combine(folder, fileNameOnly + ".csv");
                var options = new ViewScheduleExportOptions
                {
                    FieldDelimiter = ",",
                    TextQualifier = ExportTextQualifier.DoubleQuote,
                    ColumnHeaders = ExportColumnHeaders.OneRow,
                    Title = false,
                    HeadersFootersBlanks = false,
                };
                schedule.Export(folder, fileNameOnly + ".csv", options);
                var xlsxPath = BrandedXlsx.ReplaceCsvWithBrandedXlsx(csvPath, schedule.Name, doc.Title);
                TaskDialog.Show("BIMFlow — RoomFinishSchedule", $"Exported to:\n{xlsxPath}");
            }
        }

        return Result.Succeeded;
    }

    private static string MakeUniqueName(Document doc, string baseName)
    {
        var existingNames = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Select(s => s.Name)
            .ToHashSet();

        if (!existingNames.Contains(baseName)) return baseName;

        var i = 2;
        while (existingNames.Contains($"{baseName} {i}")) i++;
        return $"{baseName} {i}";
    }
}
