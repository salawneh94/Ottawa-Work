// Explicit aliases, not plain "using System.IO;" — this project has both
// UseWPF and UseWindowsForms on, and that combination drops System.IO from
// the SDK's implicit global usings (see LicenseStore.cs for the full
// explanation) — needed here for Path/File.
using Path = System.IO.Path;
using File = System.IO.File;

using System.Text;
using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

public enum ExportFormat { Csv, Excel, TabDelimited }
public enum SyncEncoding { Utf8, Utf8Bom, SystemDefault }

/// <summary>One schedule available to export, with the summary info the picker list shows.</summary>
public record ScheduleInfo(ViewSchedule Schedule, string Name, string Category, int Rows, int Columns);

/// <summary>
/// The real engine behind Excel Sync: list every schedule with its category
/// and size (matching the reference tool's own schedule browser), export a
/// batch of them with a chosen format/delimiter/encoding/filename pattern,
/// and re-import parameter values from a previously exported (and edited)
/// file. Export never touches the model — only Import does, so only Import
/// needs to run inside a transaction (see Command.cs).
/// </summary>
public static class ExcelSyncEngine
{
    public static List<ScheduleInfo> ListSchedules(Document doc)
    {
        // Revision schedules (Titleblock Revision Schedule — "Änderungsliste"
        // in a German project) and Revit's own internal keynote schedule are
        // system-generated bookkeeping views, not the kind of element/
        // quantity data this tool exports — confirmed live (user-reported):
        // they don't have a real element category (Category.GetCategory
        // returned null for them), which is what filled the category filter
        // row with unlabeled "—" chips in the first place, and they aren't
        // schedules a user would ever want to export to Excel anyway.
        // IsTitleblockRevisionSchedule/IsInternalKeynoteSchedule are Revit's
        // own flags for exactly this distinction, so excluding them here
        // both fixes the confusing chip row and stops cluttering the list
        // with schedules nobody's exporting.
        return new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule && !s.IsInternalKeynoteSchedule)
            .Select(s =>
            {
                var category = Category.GetCategory(doc, s.Definition.CategoryId)?.Name ?? "No Category";
                var (rows, cols) = ScheduleSize(s);
                return new ScheduleInfo(s, s.Name, category, rows, cols);
            })
            .OrderBy(i => i.Name)
            .ToList();
    }

    private static (int Rows, int Columns) ScheduleSize(ViewSchedule schedule)
    {
        try
        {
            var section = schedule.GetTableData().GetSectionData(SectionType.Body);
            return (section.NumberOfRows, section.NumberOfColumns);
        }
        catch (Exception)
        {
            // A schedule with no visible body rows (all filtered out, or a
            // key/legend schedule shape the table-data API doesn't expect)
            // — the size is just cosmetic in the picker, so 0/0 is fine.
            return (0, 0);
        }
    }

    /// <summary>Fills {name}/{category}/{date}/{project}/{id} tokens in a filename pattern. No extension —
    /// the caller appends the one matching the chosen export format.</summary>
    public static string ResolveFileName(string pattern, ScheduleInfo info, string projectTitle, DateTime date)
    {
        var name = pattern
            .Replace("{name}", SanitizeForFileName(info.Name))
            .Replace("{category}", SanitizeForFileName(info.Category))
            .Replace("{project}", SanitizeForFileName(projectTitle))
            .Replace("{date}", date.ToString("yyyy-MM-dd"))
            .Replace("{id}", info.Schedule.Id.Value.ToString());
        return string.IsNullOrWhiteSpace(name) ? SanitizeForFileName(info.Name) : name;
    }

    private static string SanitizeForFileName(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(text.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    public static string DelimiterChar(string delimiterLabel) => delimiterLabel switch
    {
        "Semicolon" => ";",
        "Tab" => "\t",
        _ => ",",
    };

    public static Encoding ResolveEncoding(SyncEncoding encoding) => encoding switch
    {
        SyncEncoding.Utf8Bom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        SyncEncoding.SystemDefault => Encoding.Latin1,
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    /// <summary>Exports one schedule to <paramref name="folder"/>/<paramref name="fileNameNoExt"/> in the
    /// chosen format, returning the final file path actually written. Revit's own ViewSchedule.Export
    /// always writes a delimited text file first; for Csv/TabDelimited that file (re-written in the
    /// chosen encoding) IS the output, for Excel it's converted to a branded .xlsx and the intermediate
    /// text file is discarded.</summary>
    public static string ExportOne(ViewSchedule schedule, string folder, string fileNameNoExt, ExportFormat format, string delimiter, SyncEncoding encoding, string projectTitle)
    {
        var textFileName = fileNameNoExt + (format == ExportFormat.TabDelimited ? ".txt" : ".csv");
        var options = new ViewScheduleExportOptions
        {
            // Excel's own intermediate text file always goes through
            // Csv.ParseLine on the way to a workbook (see
            // BrandedXlsx.ReplaceCsvWithBrandedXlsx), which only understands
            // comma-separated fields — so Excel output must force a comma
            // here regardless of the user's chosen delimiter, which only
            // really applies to a Csv-format output the user opens directly.
            FieldDelimiter = format switch
            {
                ExportFormat.TabDelimited => "\t",
                ExportFormat.Excel => ",",
                _ => delimiter,
            },
            TextQualifier = ExportTextQualifier.DoubleQuote,
            ColumnHeaders = ExportColumnHeaders.OneRow,
            Title = false,
            HeadersFootersBlanks = false,
        };

        schedule.Export(folder, textFileName, options);
        var textPath = Path.Combine(folder, textFileName);

        // Revit writes the delimited file in its own default encoding — re-write it in the
        // chosen one so "Encoding" is a real, honored setting rather than a decorative dropdown.
        var content = File.ReadAllText(textPath);
        File.WriteAllText(textPath, content, ResolveEncoding(encoding));

        if (format != ExportFormat.Excel) return textPath;

        return BrandedXlsx.ReplaceCsvWithBrandedXlsx(textPath, schedule.Name, projectTitle);
    }

    public record ImportResult(int UpdatedFields, int UpdatedRows);

    /// <summary>Matches each row back to an element by the table's first column (whatever that schedule's
    /// leftmost field happens to be, e.g. Mark), then writes every other column as a same-named parameter.
    /// Must run inside a transaction — this is the one operation here that touches the model.</summary>
    public static ImportResult ImportParameters(Document doc, List<string> headers, List<List<string>> rows)
    {
        const int keyIndex = 0;
        var keyHeader = headers[keyIndex];
        var paramColumns = headers.Select((h, i) => (Header: h, Index: i)).Where(t => t.Index != keyIndex).ToList();
        var updatedRows = 0;
        var updatedFields = 0;

        foreach (var fields in rows)
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

        return new ImportResult(updatedFields, updatedRows);
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
