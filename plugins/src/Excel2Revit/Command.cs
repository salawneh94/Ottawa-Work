using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;
// UseWPF+UseWindowsForms together drops System.IO from implicit global
// usings (see Shared/LicenseStore.cs) — needed here for Path/File.
using Path = System.IO.Path;
using File = System.IO.File;

namespace BIMFlow.Excel2Revit;

/// <summary>
/// Export mode: dumps a chosen schedule to a branded Excel workbook via
/// Revit's own schedule export. Import mode: drives arbitrary parameter
/// values on elements from a CSV (ElementId column + one column per
/// parameter name), generalizing the ElementId-matched import pattern
/// SheetListExporter uses for sheets to any category.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "excel2revit";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var choice = MessageBox.Show(
            "Export a schedule to Excel?\n\nChoose \"No\" to import parameter values from a CSV file instead.",
            "BIMFlow — Excel2Revit",
            MessageBoxButtons.YesNoCancel);

        if (choice == DialogResult.Cancel) return Result.Cancelled;

        return choice == DialogResult.Yes ? ExportSchedule(doc) : ImportParameters(doc);
    }

    private Result ExportSchedule(Document doc)
    {
        var schedules = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Where(s => !s.IsTemplate)
            .OrderBy(s => s.Name)
            .ToList();

        if (schedules.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Excel2Revit", "No schedules were found in this project.");
            return Result.Succeeded;
        }

        var pickerWindow = new SchedulePickerWindow(schedules);
        if (pickerWindow.ShowDialog() != true || pickerWindow.SelectedSchedule is null)
            return Result.Cancelled;

        using var saveDialog = new SaveFileDialog
        {
            Title = "Export schedule",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = $"{pickerWindow.SelectedSchedule.Name}.xlsx",
        };
        if (saveDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

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

        pickerWindow.SelectedSchedule.Export(folder, fileNameOnly + ".csv", options);
        var xlsxPath = BrandedXlsx.ReplaceCsvWithBrandedXlsx(csvPath, pickerWindow.SelectedSchedule.Name, doc.Title);

        TaskDialog.Show("BIMFlow — Excel2Revit", $"Exported \"{pickerWindow.SelectedSchedule.Name}\" to:\n{xlsxPath}");
        return Result.Succeeded;
    }

    private Result ImportParameters(Document doc)
    {
        using var openDialog = new OpenFileDialog
        {
            Title = "Import parameter values (columns: ElementId, then one column per parameter name)",
            Filter = "CSV files (*.csv)|*.csv",
        };
        if (openDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

        var lines = File.ReadAllLines(openDialog.FileName);
        if (lines.Length < 2)
        {
            TaskDialog.Show("BIMFlow — Excel2Revit", "That file has no data rows.");
            return Result.Cancelled;
        }

        var header = Csv.ParseLine(lines[0]);
        var idIndex = header.IndexOf("ElementId");
        if (idIndex < 0)
        {
            TaskDialog.Show("BIMFlow — Excel2Revit", "The CSV needs an ElementId column.");
            return Result.Cancelled;
        }

        var paramColumns = header.Select((h, i) => (Header: h, Index: i)).Where(t => t.Index != idIndex).ToList();
        var updatedRows = 0;
        var updatedFields = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Import Parameter Values");
        transaction.Start();
        try
        {
            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var fields = Csv.ParseLine(lines[i]);
                if (!long.TryParse(fields.ElementAtOrDefault(idIndex), out var rawId)) continue;

                var element = doc.GetElement(new ElementId(rawId));
                if (element is null) continue;

                var rowChanged = false;
                foreach (var (columnHeader, columnIndex) in paramColumns)
                {
                    var value = fields.ElementAtOrDefault(columnIndex);
                    if (value is null) continue;

                    var param = element.LookupParameter(columnHeader);
                    if (param is not { IsReadOnly: false }) continue;

                    try
                    {
                        switch (param.StorageType)
                        {
                            case StorageType.String: param.Set(value); break;
                            case StorageType.Integer: param.Set(int.Parse(value)); break;
                            case StorageType.Double: param.Set(double.Parse(value)); break;
                            default: continue;
                        }
                        rowChanged = true;
                        updatedFields++;
                    }
                    catch (Exception)
                    {
                        // Value doesn't fit the parameter's type — skip that one field.
                    }
                }

                if (rowChanged) updatedRows++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — Excel2Revit", $"Updated {updatedFields} field(s) across {updatedRows} element(s).");
        return Result.Succeeded;
    }
}
