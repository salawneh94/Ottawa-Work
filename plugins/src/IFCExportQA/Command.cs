using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;
// UseWPF+UseWindowsForms together drops System.IO from implicit global
// usings (see Shared/LicenseStore.cs) — needed here for Path.
using Path = System.IO.Path;

namespace BIMFlow.IFCExportQA;

/// <summary>
/// Runs Revit's own IFC export with a chosen version, after showing a
/// pre-flight summary of what's about to be exported by category. Deep
/// validation (checking specific IFC property-set completeness per
/// element) needs the same schema knowledge a full IFC QA tool would — this
/// covers the "know what you're about to export, then export it cleanly"
/// half confidently.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "ifcexportqa";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var categorySummary = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e.Category is not null && e.Category.CategoryType == CategoryType.Model)
            .GroupBy(e => e.Category!.Name)
            .Select(g => (Category: g.Key, Count: g.Count()))
            .ToList();

        if (categorySummary.Count == 0)
        {
            TaskDialog.Show("BIMFlow — IFCExportQA", "No model elements were found to export.");
            return Result.Succeeded;
        }

        var window = new IFCExportWindow(categorySummary);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        using var saveDialog = new SaveFileDialog
        {
            Title = "Export IFC",
            Filter = "IFC files (*.ifc)|*.ifc",
            FileName = $"{Path.GetFileNameWithoutExtension(doc.PathName)}.ifc",
        };
        if (saveDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

        var options = new IFCExportOptions { FileVersion = window.SelectedVersion };
        var folder = Path.GetDirectoryName(saveDialog.FileName)!;
        var fileNameOnly = Path.GetFileNameWithoutExtension(saveDialog.FileName);

        using var transaction = new Transaction(doc, "BIMFlow: Export IFC");
        transaction.Start();
        try
        {
            doc.Export(folder, fileNameOnly, options);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("BIMFlow — IFCExportQA", $"Export failed: {ex.Message}");
            return Result.Failed;
        }

        TaskDialog.Show("BIMFlow — IFCExportQA", $"Exported to:\n{saveDialog.FileName}");
        return Result.Succeeded;
    }
}
