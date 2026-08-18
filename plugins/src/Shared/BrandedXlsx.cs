// Explicit aliases, not a plain "using System.IO;" — Shared has both
// UseWindowsForms and UseWPF, and that combination drops System.IO from
// the SDK's implicit global usings (see LicenseStore.cs for the full
// explanation).
using Path = System.IO.Path;
using File = System.IO.File;

using System.Reflection;
using System.Windows.Forms;
using ClosedXML.Excel;

namespace BIMFlow.Shared;

/// <summary>
/// Writes real .xlsx reports styled to Ottawa Ingenieure's own table
/// convention (verified against the company's PPTX template and an actual
/// slide export), instead of the plain CSV every export plugin used to
/// write: the wordmark logo, a thin blue-to-green accent bar, a blue
/// all-caps title, a solid green header row with black bold text, and
/// gray/white banded data rows. Blue (#009FE3) and green (#A0C519) are the
/// only two colors actually used across the template's slides — its other
/// theme accents (teal, etc.) never appear on an actual slide, so they're
/// deliberately not used here either.
/// </summary>
public static class BrandedXlsx
{
    private const string Blue = "009FE3";
    private const string Green = "A0C519";
    private const string Black = "000000";
    private const string BandGray = "D9D9D9";
    private const string BorderGray = "BFBFBF";
    private const string GrayText = "878787";
    private const double LogoAspectRatio = 1200.0 / 364.0;

    /// <summary>
    /// Prompts for a save location and writes a branded .xlsx built directly
    /// from the given rows. Returns the chosen path, or null if the user
    /// cancelled the save dialog.
    /// </summary>
    public static string? Save(
        string dialogTitle,
        string suggestedFileName,
        string sheetTitle,
        string? subtitle,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        using var saveDialog = new SaveFileDialog
        {
            Title = dialogTitle,
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = Path.ChangeExtension(suggestedFileName, ".xlsx"),
        };
        if (saveDialog.ShowDialog() != DialogResult.OK) return null;

        using var workbook = Build(sheetTitle, subtitle, headers, rows);
        workbook.SaveAs(saveDialog.FileName);
        return saveDialog.FileName;
    }

    /// <summary>
    /// For the plugins that hand schedule export off to Revit's own
    /// ViewSchedule.Export (which controls the resulting column list, so we
    /// can't build rows ourselves): re-parses the CSV Revit already wrote,
    /// re-renders it as a branded workbook at the same path with a .xlsx
    /// extension, and deletes the intermediate CSV. Returns the .xlsx path.
    /// </summary>
    public static string ReplaceCsvWithBrandedXlsx(string csvPath, string sheetTitle, string? subtitle)
    {
        var xlsxPath = Path.ChangeExtension(csvPath, ".xlsx");
        var lines = File.ReadAllLines(csvPath);

        var headers = lines.Length > 0 ? Csv.ParseLine(lines[0]) : new List<string>();
        var rows = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).Select(Csv.ParseLine).ToList();

        using (var workbook = Build(sheetTitle, subtitle, headers, rows))
            workbook.SaveAs(xlsxPath);

        File.Delete(csvPath);
        return xlsxPath;
    }

    /// <summary>
    /// Reads a workbook this class previously saved back into a header row
    /// plus data rows, for plugins that re-import their own export (e.g.
    /// SheetListExporter). Scans for the row containing <paramref
    /// name="headerAnchor"/> (e.g. "ElementId") rather than assuming row 1,
    /// since the branded layout has logo/title rows above the real header.
    /// Returns null if no row contains the anchor.
    /// </summary>
    public static (List<string> Headers, List<List<string>> Rows)? ReadTable(string xlsxPath, string headerAnchor)
    {
        using var workbook = new XLWorkbook(xlsxPath);
        var ws = workbook.Worksheets.First();

        var headerRow = ws.RowsUsed().FirstOrDefault(r => r.Cells().Any(c => c.GetString() == headerAnchor));
        if (headerRow is null) return null;

        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        var headers = Enumerable.Range(1, lastCol).Select(c => headerRow.Cell(c).GetString()).ToList();

        var rows = ws.RowsUsed()
            .Where(r => r.RowNumber() > headerRow.RowNumber())
            .Select(r => Enumerable.Range(1, lastCol).Select(c => r.Cell(c).GetString()).ToList())
            .Where(values => values.Any(v => !string.IsNullOrWhiteSpace(v)))
            .ToList();

        return (headers, rows);
    }

    private static XLWorkbook Build(
        string sheetTitle,
        string? subtitle,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(SanitizeSheetName(sheetTitle));
        ws.ShowGridLines = false;

        var lastCol = Math.Max(headers.Count, 1);

        var logoPath = FindLogoPath();
        if (logoPath is not null)
        {
            const int logoHeight = 32;
            ws.AddPicture(logoPath).MoveTo(ws.Cell(1, 1)).WithSize((int)(logoHeight * LogoAspectRatio), logoHeight);
        }
        ws.Row(1).Height = 26;

        // ClosedXML has no gradient cell fill, so approximate the logo's
        // blue -> green sweep by coloring each column along a thin bar.
        for (var col = 1; col <= lastCol; col++)
        {
            var t = lastCol == 1 ? 0 : (double)(col - 1) / (lastCol - 1);
            ws.Cell(2, col).Style.Fill.BackgroundColor = XLColor.FromHtml(LerpHex(Blue, Green, t));
        }
        ws.Row(2).Height = 3;

        ws.Cell(3, 1).Value = sheetTitle.ToUpperInvariant();
        ws.Range(3, 1, 3, lastCol).Merge();
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 1).Style.Font.FontSize = 14;
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml($"#{Blue}");
        ws.Cell(3, 1).Style.Font.FontName = "Arial";
        ws.Row(3).Height = 20;

        var headerRow = 4;
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            ws.Cell(4, 1).Value = subtitle;
            ws.Range(4, 1, 4, lastCol).Merge();
            ws.Cell(4, 1).Style.Font.Italic = true;
            ws.Cell(4, 1).Style.Font.FontColor = XLColor.FromHtml($"#{GrayText}");
            ws.Cell(4, 1).Style.Font.FontSize = 9;
            ws.Cell(4, 1).Style.Font.FontName = "Arial";
            ws.Row(4).Height = 14;
            headerRow = 5;
        }

        for (var col = 1; col <= lastCol; col++)
        {
            var cell = ws.Cell(headerRow, col);
            cell.Value = col <= headers.Count ? headers[col - 1] : "";
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.FromHtml($"#{Black}");
            cell.Style.Font.FontName = "Arial";
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml($"#{Green}");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml($"#{BorderGray}");
        }
        ws.SheetView.FreezeRows(headerRow);

        var dataRow = headerRow + 1;
        var band = false;
        foreach (var row in rows)
        {
            for (var col = 1; col <= lastCol; col++)
            {
                var cell = ws.Cell(dataRow, col);
                cell.Value = col <= row.Count ? row[col - 1] : "";
                cell.Style.Font.FontName = "Arial";
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml($"#{BorderGray}");
                if (band) cell.Style.Fill.BackgroundColor = XLColor.FromHtml($"#{BandGray}");
            }
            dataRow++;
            band = !band;
        }

        ws.Columns(1, lastCol).AdjustToContents();
        return workbook;
    }

    /// <summary>
    /// Looks up the logo next to this DLL's own location, in a "Branding"
    /// subfolder — same convention as RibbonBuilder.ApplyIcon's "Icons"
    /// lookup (see the CI packaging step that copies both alongside every
    /// DLL). Missing file is skipped silently; the report still works, just
    /// without the logo.
    /// </summary>
    private static string? FindLogoPath()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var logoPath = Path.Combine(Path.GetDirectoryName(assemblyLocation)!, "Branding", "ottawa-logo.png");
        return File.Exists(logoPath) ? logoPath : null;
    }

    private static string LerpHex(string fromHex, string toHex, double t)
    {
        var (r1, g1, b1) = (Convert.ToInt32(fromHex[..2], 16), Convert.ToInt32(fromHex[2..4], 16), Convert.ToInt32(fromHex[4..6], 16));
        var (r2, g2, b2) = (Convert.ToInt32(toHex[..2], 16), Convert.ToInt32(toHex[2..4], 16), Convert.ToInt32(toHex[4..6], 16));
        var r = (int)(r1 + (r2 - r1) * t);
        var g = (int)(g1 + (g2 - g1) * t);
        var b = (int)(b1 + (b2 - b1) * t);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? ' ' : c).ToArray()).Trim();
        if (cleaned.Length > 31) cleaned = cleaned[..31];
        return cleaned.Length == 0 ? "Sheet1" : cleaned;
    }
}
