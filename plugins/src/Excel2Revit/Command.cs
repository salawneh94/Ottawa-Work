using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;
using System.Windows.Forms;
// UseWPF+UseWindowsForms together drops System.IO from implicit global
// usings (see Shared/LicenseStore.cs) — needed here for Path.
using Path = System.IO.Path;

namespace OttawaWork.Excel2Revit;

/// <summary>
/// Export mode: dumps a chosen schedule to a branded Excel workbook via
/// Revit's own schedule export, completely unmodified — Revit's schedule
/// API doesn't reliably let a plugin add an Element ID column to every
/// category's schedule (confirmed against a real project: no schedulable
/// field for BuiltInParameter.ID_PARAM, or with "element" anywhere in its
/// name, existed for that schedule's category at all), so this doesn't
/// try. Import mode instead matches rows back to elements by the
/// schedule's own first column — whatever that schedule happens to be
/// showing as its leftmost field (e.g. "Kennzeichen"/Mark for a door or
/// window schedule) — via LookupParameter on that same column name,
/// since firms already rely on that column being unique per row (that's
/// the whole point of Mark numbering) rather than via Element ID.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "excel2revit";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var choice = MessageBox.Show(
            "Export a schedule to Excel?\n\nChoose \"No\" to import parameter values from an edited export instead.",
            "Ottawa Tools — Excel2Revit",
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
            TaskDialog.Show("Ottawa Tools — Excel2Revit", "No schedules were found in this project.");
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

        TaskDialog.Show("Ottawa Tools — Excel2Revit", $"Exported \"{pickerWindow.SelectedSchedule.Name}\" to:\n{xlsxPath}");
        return Result.Succeeded;
    }

    private Result ImportParameters(Document doc)
    {
        using var openDialog = new OpenFileDialog
        {
            Title = "Import parameter values (matched by the first column, e.g. Mark; other columns are parameter names)",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
        };
        if (openDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

        var table = BrandedXlsx.ReadTable(openDialog.FileName);
        if (table is null || table.Value.Headers.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — Excel2Revit", "This file doesn't look like a Batch Excel Sync export — no header row was found.");
            return Result.Cancelled;
        }

        var (header, dataRows) = table.Value;
        const int keyIndex = 0;
        var keyHeader = header[keyIndex];
        var paramColumns = header.Select((h, i) => (Header: h, Index: i)).Where(t => t.Index != keyIndex).ToList();
        var updatedRows = 0;
        var updatedFields = 0;

        using var transaction = new Transaction(doc, "Ottawa Tools: Import Parameter Values");
        transaction.Start();
        try
        {
            foreach (var fields in dataRows)
            {
                var keyValue = fields.ElementAtOrDefault(keyIndex);
                if (string.IsNullOrWhiteSpace(keyValue)) continue;

                var element = FindElementByParameterValue(doc, keyHeader, keyValue);
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

        TaskDialog.Show("Ottawa Tools — Excel2Revit", $"Updated {updatedFields} field(s) across {updatedRows} element(s).");
        return Result.Succeeded;
    }

    private static Element? FindElementByParameterValue(Document doc, string paramName, string value)
    {
        return new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .FirstOrDefault(e =>
            {
                var p = e.LookupParameter(paramName);
                if (p is null) return false;
                var text = p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
                return text == value;
            });
    }
}
