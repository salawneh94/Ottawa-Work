using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;

namespace BIMFlow.SheetListExporter;

[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "sheetlistexporter";

    private static readonly (string Header, BuiltInParameter Param)[] ExtraColumns =
    {
        ("IssueDate", BuiltInParameter.SHEET_ISSUE_DATE),
        ("DrawnBy", BuiltInParameter.SHEET_DRAWN_BY),
        ("CheckedBy", BuiltInParameter.SHEET_CHECKED_BY),
        ("DesignedBy", BuiltInParameter.SHEET_DESIGNED_BY),
    };

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var choice = MessageBox.Show(
            "Export the current sheet list to Excel?\n\nChoose \"No\" to import edits from an exported file instead.",
            "BIMFlow — Sheet List Exporter",
            MessageBoxButtons.YesNoCancel);

        if (choice == DialogResult.Cancel) return Result.Cancelled;

        if (choice == DialogResult.Yes)
            return ExportXlsx(doc);

        return ImportXlsx(doc);
    }

    private Result ExportXlsx(Document doc)
    {
        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .OrderBy(s => s.SheetNumber)
            .ToList();

        var headers = new[] { "ElementId", "SheetNumber", "SheetName" }.Concat(ExtraColumns.Select(c => c.Header)).ToList();

        var rows = sheets.Select(sheet =>
        {
            var values = new List<string> { sheet.Id.ToString(), sheet.SheetNumber, sheet.Name };
            values.AddRange(ExtraColumns.Select(c => sheet.get_Parameter(c.Param)?.AsString() ?? string.Empty));
            return values;
        }).ToList();

        var path = BrandedXlsx.Save(
            "Export sheet list",
            "sheet-list.xlsx",
            "Sheet List",
            doc.Title,
            headers,
            rows);
        if (path is null) return Result.Cancelled;

        TaskDialog.Show("BIMFlow — Sheet List Exporter", $"Exported {sheets.Count} sheet(s) to:\n{path}");
        return Result.Succeeded;
    }

    private Result ImportXlsx(Document doc)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import sheet list edits",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
        };
        if (dialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

        var table = BrandedXlsx.ReadTable(dialog.FileName);
        if (table is null)
        {
            TaskDialog.Show("BIMFlow — Sheet List Exporter", "This file doesn't look like a BIMFlow sheet list export.");
            return Result.Cancelled;
        }

        var (header, dataRows) = table.Value;
        var idIndex = header.IndexOf("ElementId");
        var numberIndex = header.IndexOf("SheetNumber");
        var nameIndex = header.IndexOf("SheetName");

        if (idIndex < 0 || numberIndex < 0 || nameIndex < 0)
        {
            TaskDialog.Show("BIMFlow — Sheet List Exporter", "This file doesn't look like a BIMFlow sheet list export.");
            return Result.Cancelled;
        }

        var extraIndexes = ExtraColumns.Select(c => (c.Param, Index: header.IndexOf(c.Header))).Where(t => t.Index >= 0).ToList();

        var numberChanges = new List<(Action<string> setValue, string newValue)>();
        var otherChanges = new List<Action>();
        var updated = 0;

        foreach (var fields in dataRows)
        {
            if (!long.TryParse(fields[idIndex], out var rawId)) continue;

            var sheet = doc.GetElement(new ElementId(rawId)) as ViewSheet;
            if (sheet is null) continue;

            var newNumber = fields[numberIndex];
            var newName = fields[nameIndex];

            if (newNumber != sheet.SheetNumber)
                numberChanges.Add((v => sheet.SheetNumber = v, newNumber));
            if (newName != sheet.Name)
                otherChanges.Add(() => sheet.Name = newName);

            foreach (var (param, index) in extraIndexes)
            {
                var value = fields[index];
                otherChanges.Add(() =>
                {
                    var p = sheet.get_Parameter(param);
                    if (p is { IsReadOnly: false }) p.Set(value);
                });
            }

            updated++;
        }

        using var transaction = new Transaction(doc, "BIMFlow: Import Sheet List");
        transaction.Start();
        try
        {
            TwoPassRenamer.Apply(numberChanges);
            foreach (var change in otherChanges) change();
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — Sheet List Exporter", $"Updated {updated} sheet(s) from the file.");
        return Result.Succeeded;
    }
}
