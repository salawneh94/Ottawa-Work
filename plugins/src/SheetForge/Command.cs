using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;

namespace BIMFlow.SheetForge;

/// <summary>
/// Batch-creates sheets from a CSV list (SheetNumber, SheetName, and
/// optionally TitleBlockFamily, TitleBlockType, ViewName). Falls back to
/// the first loaded title block type when none is specified, and places
/// the named view at a fixed point on the sheet when given.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "sheetforge";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        using var dialog = new OpenFileDialog
        {
            Title = "Select sheet list CSV (columns: SheetNumber, SheetName, TitleBlockFamily, TitleBlockType, ViewName)",
            Filter = "CSV files (*.csv)|*.csv",
        };
        if (dialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

        var lines = File.ReadAllLines(dialog.FileName);
        if (lines.Length < 2)
        {
            TaskDialog.Show("BIMFlow — SheetForge", "That file has no data rows.");
            return Result.Cancelled;
        }

        var header = Csv.ParseLine(lines[0]);
        var numberIndex = header.IndexOf("SheetNumber");
        var nameIndex = header.IndexOf("SheetName");
        var tbFamilyIndex = header.IndexOf("TitleBlockFamily");
        var tbTypeIndex = header.IndexOf("TitleBlockType");
        var viewIndex = header.IndexOf("ViewName");

        if (numberIndex < 0 || nameIndex < 0)
        {
            TaskDialog.Show("BIMFlow — SheetForge", "The CSV needs at least SheetNumber and SheetName columns.");
            return Result.Cancelled;
        }

        var titleBlockTypes = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsElementType()
            .Cast<FamilySymbol>()
            .ToList();

        if (titleBlockTypes.Count == 0)
        {
            TaskDialog.Show("BIMFlow — SheetForge", "No title block types are loaded in this project.");
            return Result.Cancelled;
        }

        var defaultTitleBlock = titleBlockTypes[0];

        var views = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate)
            .ToList();

        var created = 0;
        var skipped = new List<string>();

        using var transaction = new Transaction(doc, "BIMFlow: Batch-Create Sheets");
        transaction.Start();
        try
        {
            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var fields = Csv.ParseLine(lines[i]);

                var sheetNumber = fields.ElementAtOrDefault(numberIndex) ?? "";
                var sheetName = fields.ElementAtOrDefault(nameIndex) ?? "";
                if (string.IsNullOrWhiteSpace(sheetNumber))
                {
                    skipped.Add($"Row {i + 1}: missing SheetNumber");
                    continue;
                }

                var titleBlock = defaultTitleBlock;
                if (tbFamilyIndex >= 0 && tbTypeIndex >= 0)
                {
                    var familyName = fields.ElementAtOrDefault(tbFamilyIndex);
                    var typeName = fields.ElementAtOrDefault(tbTypeIndex);
                    if (!string.IsNullOrWhiteSpace(familyName) && !string.IsNullOrWhiteSpace(typeName))
                    {
                        var match = titleBlockTypes.FirstOrDefault(t =>
                            t.Family.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                            t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
                        if (match is not null) titleBlock = match;
                    }
                }

                if (!titleBlock.IsActive) titleBlock.Activate();

                ViewSheet sheet;
                try
                {
                    sheet = ViewSheet.Create(doc, titleBlock.Id);
                }
                catch (Exception)
                {
                    skipped.Add($"Row {i + 1} ({sheetNumber}): couldn't create sheet");
                    continue;
                }

                try
                {
                    sheet.SheetNumber = sheetNumber;
                }
                catch (Exception)
                {
                    skipped.Add($"Row {i + 1}: sheet number \"{sheetNumber}\" already exists");
                    doc.Delete(sheet.Id);
                    continue;
                }
                sheet.Name = string.IsNullOrWhiteSpace(sheetName) ? sheetNumber : sheetName;

                if (viewIndex >= 0)
                {
                    var viewName = fields.ElementAtOrDefault(viewIndex);
                    if (!string.IsNullOrWhiteSpace(viewName))
                    {
                        var view = views.FirstOrDefault(v => v.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase));
                        if (view is not null && Viewport.CanAddViewToSheet(doc, sheet.Id, view.Id))
                            Viewport.Create(doc, sheet.Id, view.Id, new XYZ(1.5, 1.0, 0));
                    }
                }

                created++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        var summary = $"Created {created} sheet(s).";
        if (skipped.Count > 0)
            summary += $"\n\nSkipped {skipped.Count}:\n" + string.Join('\n', skipped.Take(10));

        TaskDialog.Show("BIMFlow — SheetForge", summary);
        return Result.Succeeded;
    }
}
