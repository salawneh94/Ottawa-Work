using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;

namespace BIMFlow.ExcelAsDraft;

/// <summary>
/// Draws a CSV's rows and columns as an actual grid (detail lines + text
/// notes) inside a drafting view — for tables that just need to be
/// readable on a sheet, no category or schedule required. Re-running on a
/// view of the same name clears and redraws it, acting as a refresh.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "excelasdraft";
    private const double ColumnWidthFeet = 1.6;
    private const double RowHeightFeet = 0.35;

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        using var dialog = new OpenFileDialog
        {
            Title = "Select CSV to draw as a table",
            Filter = "CSV files (*.csv)|*.csv",
        };
        if (dialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

        var lines = File.ReadAllLines(dialog.FileName);
        if (lines.Length == 0)
        {
            TaskDialog.Show("BIMFlow — ExcelAsDraft", "That file is empty.");
            return Result.Cancelled;
        }

        var rows = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(Csv.ParseLine).ToList();
        var columnCount = rows.Max(r => r.Count);

        var viewName = $"Excel Draft — {Path.GetFileNameWithoutExtension(dialog.FileName)}";
        var textTypeId = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).FirstElementId();

        using var transaction = new Transaction(doc, "BIMFlow: Draw Excel Table");
        transaction.Start();
        try
        {
            var view = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewDrafting))
                .Cast<ViewDrafting>()
                .FirstOrDefault(v => v.Name == viewName);

            if (view is null)
            {
                var draftingViewType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .First(t => t.ViewFamily == ViewFamily.Drafting);
                view = ViewDrafting.Create(doc, draftingViewType.Id);
                view.Name = viewName;
            }
            else
            {
                var existing = new FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType()
                    .Where(e => e is DetailLine or TextNote)
                    .Select(e => e.Id)
                    .ToList();
                if (existing.Count > 0) doc.Delete(existing);
            }

            for (var r = 0; r <= rows.Count; r++)
            {
                var y = -r * RowHeightFeet;
                DrawLine(doc, view, new XYZ(0, y, 0), new XYZ(columnCount * ColumnWidthFeet, y, 0));
            }
            for (var c = 0; c <= columnCount; c++)
            {
                var x = c * ColumnWidthFeet;
                DrawLine(doc, view, new XYZ(x, 0, 0), new XYZ(x, -rows.Count * RowHeightFeet, 0));
            }

            if (textTypeId is not null)
            {
                for (var r = 0; r < rows.Count; r++)
                {
                    for (var c = 0; c < rows[r].Count; c++)
                    {
                        var text = rows[r][c];
                        if (string.IsNullOrWhiteSpace(text)) continue;

                        var point = new XYZ(c * ColumnWidthFeet + 0.05, -r * RowHeightFeet - RowHeightFeet + 0.08, 0);
                        TextNote.Create(doc, view.Id, point, text, textTypeId);
                    }
                }
            }

            transaction.Commit();
            uiDoc.ActiveView = view;
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — ExcelAsDraft", $"Drew a {rows.Count}×{columnCount} table in \"{viewName}\".");
        return Result.Succeeded;
    }

    private static void DrawLine(Document doc, View view, XYZ start, XYZ end)
    {
        if (start.DistanceTo(end) < 0.001) return;
        doc.Create.NewDetailCurve(view, Line.CreateBound(start, end));
    }
}
