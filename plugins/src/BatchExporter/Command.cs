using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
// UseWPF+UseWindowsForms together drops System.IO from implicit global
// usings (see Shared/LicenseStore.cs) — needed here for Directory/File/Path.
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace BIMFlow.BatchExporter;

[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "batchexporter";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var candidateViews = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate && v.CanBePrinted)
            .OrderBy(v => v is ViewSheet sheet ? sheet.SheetNumber : v.Name)
            .ToList();

        var window = new BatchExporterWindow(candidateViews);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedViews.Count == 0)
            return Result.Succeeded;

        if (string.IsNullOrWhiteSpace(window.OutputFolder) || !Directory.Exists(window.OutputFolder))
        {
            TaskDialog.Show("BIMFlow — Batch Exporter", "Pick a valid output folder first.");
            return Result.Cancelled;
        }

        var extension = window.Format == ExportFormat.Pdf ? ".pdf" : ".dwg";
        var exported = 0;
        var failed = new List<string>();

        for (var i = 0; i < window.SelectedViews.Count; i++)
        {
            var view = window.SelectedViews[i];
            var fileName = NamingTemplate.Resolve(window.Template, view, i + 1);

            try
            {
                ExportOneView(doc, view, window.OutputFolder, fileName, window.Format);
                exported++;
            }
            catch (Exception)
            {
                failed.Add(view.Name);
            }
        }

        var summary = $"Exported {exported} of {window.SelectedViews.Count} file(s) to:\n{window.OutputFolder}";
        if (failed.Count > 0)
            summary += $"\n\nFailed: {string.Join(", ", failed)}";

        TaskDialog.Show("BIMFlow — Batch Exporter", summary);
        return Result.Succeeded;
    }

    /// <summary>
    /// Exports a single view/sheet and guarantees the output file matches
    /// the requested name, regardless of the suffix Revit's own export
    /// commands may append internally for a given format.
    /// </summary>
    private static void ExportOneView(Document doc, View view, string folder, string desiredFileName, ExportFormat format)
    {
        var before = Directory.GetFiles(folder).ToHashSet();

        if (format == ExportFormat.Pdf)
        {
            var options = new PDFExportOptions { Combine = true, FileName = desiredFileName };
            doc.Export(folder, new List<ElementId> { view.Id }, options);
        }
        else
        {
            var options = new DWGExportOptions();
            doc.Export(folder, desiredFileName, new List<ElementId> { view.Id }, options);
        }

        var after = Directory.GetFiles(folder);
        var newFile = after.FirstOrDefault(f => !before.Contains(f));
        var extension = format == ExportFormat.Pdf ? ".pdf" : ".dwg";
        var desiredPath = Path.Combine(folder, desiredFileName + extension);

        if (newFile is not null && !string.Equals(newFile, desiredPath, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(desiredPath)) File.Delete(desiredPath);
            File.Move(newFile, desiredPath);
        }
    }
}
