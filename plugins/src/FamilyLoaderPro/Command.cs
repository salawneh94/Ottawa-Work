using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;

namespace BIMFlow.FamilyLoaderPro;

/// <summary>
/// Loads every .rfa file in a chosen folder (and its subfolders) in one
/// pass, reporting which families loaded clean, which already existed in
/// the project and were overwritten, and which failed to load.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "familyloaderpro";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        using var folderDialog = new FolderBrowserDialog { Description = "Choose a folder of families (.rfa) to load" };
        if (folderDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

        var files = Directory.GetFiles(folderDialog.SelectedPath, "*.rfa", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            TaskDialog.Show("BIMFlow — FamilyLoaderPro", "No .rfa files were found in that folder.");
            return Result.Succeeded;
        }

        var loaded = 0;
        var overwritten = 0;
        var failed = new List<string>();

        using var transaction = new Transaction(doc, "BIMFlow: Batch Load Families");
        transaction.Start();
        try
        {
            foreach (var file in files)
            {
                var loadOptions = new ConflictTrackingLoadOptions();
                try
                {
                    var success = doc.LoadFamily(file, loadOptions, out _);
                    if (success)
                    {
                        loaded++;
                        if (loadOptions.ConflictFound) overwritten++;
                    }
                    else
                    {
                        failed.Add(Path.GetFileName(file));
                    }
                }
                catch (Exception)
                {
                    failed.Add(Path.GetFileName(file));
                }
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        var summary = $"Loaded {loaded} of {files.Length} family file(s) ({overwritten} overwrote an existing family).";
        if (failed.Count > 0)
            summary += $"\n\nFailed: {string.Join(", ", failed)}";

        TaskDialog.Show("BIMFlow — FamilyLoaderPro", summary);
        return Result.Succeeded;
    }

    private class ConflictTrackingLoadOptions : IFamilyLoadOptions
    {
        public bool ConflictFound { get; private set; }

        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            ConflictFound = true;
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            ConflictFound = true;
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}
