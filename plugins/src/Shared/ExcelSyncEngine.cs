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

    /// <summary>Every visible field's column heading, in field order — exactly what
    /// ViewScheduleExportOptions.ColumnHeaders = OneRow writes as an export's header row, so this is how a
    /// previously-exported file gets re-associated with its live source schedule at import time (see
    /// FindSourceSchedule).</summary>
    public static List<string> FieldHeaders(ViewSchedule schedule)
    {
        var definition = schedule.Definition;
        var headers = new List<string>();
        for (var i = 0; i < definition.GetFieldCount(); i++)
        {
            var field = definition.GetField(i);
            if (field.IsHidden) continue;
            headers.Add(field.ColumnHeading);
        }
        return headers;
    }

    /// <summary>Finds which of the model's schedules produced a given exported header row. A pure header-
    /// text match isn't reliable on its own — firm templates commonly reuse the exact same shared-parameter
    /// columns (O-G_Etage, O-G_Feuerwiderstandsklasse, ...) across totally different categories, so two
    /// unrelated schedules (e.g. a door schedule and a structural-column schedule) can have byte-identical
    /// column headings. Picking merely the first such match by name order silently chose the wrong schedule
    /// outright — confirmed live (user-reported): a door-schedule import reported real field updates, but
    /// none of the actually-edited door cells changed, because the update landed on a same-headers
    /// structural schedule's 165 elements instead. When more than one schedule's headers match, this
    /// disambiguates by checking the DATA: for each candidate, how many of the imported rows' key values
    /// (see FindKeyFieldIndex) actually resolve to a real element in that candidate's own category — the
    /// true source schedule matches nearly all of them, an unrelated same-headers schedule matches
    /// essentially none. Returns null if no schedule's headers match at all (renamed/deleted schedule, or a
    /// hand-edited header row).</summary>
    public static ViewSchedule? FindSourceSchedule(Document doc, List<string> importedHeaders, List<List<string>> rows)
    {
        var headerMatches = ListSchedules(doc)
            .Select(info => info.Schedule)
            .Where(s => FieldHeaders(s).SequenceEqual(importedHeaders, StringComparer.Ordinal))
            .ToList();

        if (headerMatches.Count <= 1) return headerMatches.FirstOrDefault();

        ViewSchedule? best = null;
        var bestScore = -1;
        foreach (var schedule in headerMatches)
        {
            var keyIndex = FindKeyFieldIndex(schedule);
            var keyHeader = importedHeaders.ElementAtOrDefault(keyIndex);
            if (keyHeader is null) continue;

            var categoryId = schedule.Definition.CategoryId;
            var candidates = categoryId != ElementId.InvalidElementId
                ? new FilteredElementCollector(doc).OfCategoryId(categoryId).WhereElementIsNotElementType()
                : new FilteredElementCollector(doc).WhereElementIsNotElementType();
            var realValues = candidates
                .Select(e => e.LookupParameter(keyHeader))
                .Where(p => p is not null)
                .Select(p => p!.StorageType == StorageType.String ? p.AsString() : p.AsValueString())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToHashSet();

            var sample = rows.Select(r => r.ElementAtOrDefault(keyIndex)).Where(v => !string.IsNullOrWhiteSpace(v)).Take(20).ToList();
            if (sample.Count == 0) continue;
            var score = sample.Count(v => realValues.Contains(v!));
            if (score > bestScore) { bestScore = score; best = schedule; }
        }

        return best ?? headerMatches[0];
    }

    /// <summary>The index (within FieldHeaders' own order) of the first field safe to use as the
    /// row-matching key: one backed by a real per-element parameter (Instance or ElementType), skipping
    /// calculated/aggregate fields. Count, Formula, and CombinedParameter fields never carry a real
    /// per-element value — matching against them either finds nothing (every element's LookupParameter
    /// comes back null, so nothing ever gets updated) or, worse, silently matches the same wrong element
    /// for every row. Confirmed live (user-reported): a door schedule's first column was the built-in Count
    /// field ("Anzahl" in this German project), so blindly treating column 0 as the key produced zero real
    /// matches on any row and no visible error. Falls back to 0 if the schedule has no such field at all.</summary>
    public static int FindKeyFieldIndex(ViewSchedule schedule)
    {
        var definition = schedule.Definition;
        var visibleIndex = -1;
        for (var i = 0; i < definition.GetFieldCount(); i++)
        {
            var field = definition.GetField(i);
            if (field.IsHidden) continue;
            visibleIndex++;
            if (field.IsCalculatedField) continue;
            if (field.FieldType is ScheduleFieldType.Instance or ScheduleFieldType.ElementType && field.ParameterId != ElementId.InvalidElementId)
                return visibleIndex;
        }
        return 0;
    }

    public record ImportResult(int UpdatedFields, int UpdatedRows);

    /// <summary>Matches each row back to an element by a key column, then writes every other column as a
    /// same-named parameter. When <paramref name="sourceSchedule"/> is known (the normal case — see
    /// FindSourceSchedule), the key column is the schedule's own first real parameter field (see
    /// FindKeyFieldIndex) rather than blindly column 0, and element candidates are narrowed to the
    /// schedule's own category and indexed into a dictionary once, instead of a full unfiltered
    /// document scan repeated for every row (confirmed live: on a large model this made import slow enough
    /// to look hung, on top of it silently matching nothing). Without a matched schedule this falls back to
    /// the old column-0 behavior. Must run inside a transaction — this is the one operation here that
    /// touches the model.</summary>
    public static ImportResult ImportParameters(Document doc, List<string> headers, List<List<string>> rows, ViewSchedule? sourceSchedule)
    {
        var keyIndex = sourceSchedule is not null ? FindKeyFieldIndex(sourceSchedule) : 0;
        if (keyIndex >= headers.Count) keyIndex = 0;
        var keyHeader = headers[keyIndex];
        var paramColumns = headers.Select((h, i) => (Header: h, Index: i)).Where(t => t.Index != keyIndex).ToList();

        var categoryId = sourceSchedule?.Definition.CategoryId;
        var candidates = categoryId is { } cid && cid != ElementId.InvalidElementId
            ? new FilteredElementCollector(doc).OfCategoryId(cid).WhereElementIsNotElementType()
            : new FilteredElementCollector(doc).WhereElementIsNotElementType();

        var byKey = new Dictionary<string, Element>();
        foreach (var element in candidates)
        {
            var p = element.LookupParameter(keyHeader);
            if (p is null) continue;
            var text = p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
            if (string.IsNullOrEmpty(text) || byKey.ContainsKey(text)) continue;
            byKey[text] = element;
        }

        var updatedRows = 0;
        var updatedFields = 0;

        foreach (var fields in rows)
        {
            var keyValue = fields.ElementAtOrDefault(keyIndex);
            if (string.IsNullOrWhiteSpace(keyValue)) continue;
            if (!byKey.TryGetValue(keyValue, out var element)) continue;

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
}
