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
/// pre-flight summary of what's about to be exported by category and an
/// optional IFC class mapping step (family/type name → "Export Type to
/// IFC As", e.g. "Steckdose" → "IfcOutlet") for families Revit's default
/// category mapping can't distinguish between on its own. Deep validation
/// (checking specific IFC property-set completeness per element) needs the
/// same schema knowledge a full IFC QA tool would — this covers the "know
/// what you're about to export, map families that need it, then export
/// cleanly" half confidently.
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

        var mappingWindow = new IfcClassMappingWindow();
        if (mappingWindow.ShowDialog() != true)
            return Result.Cancelled;

        // Declared here (not inside the block below) so the post-export
        // rewrite step further down can reuse the exact same match/ownership
        // data instead of recomputing it.
        var matchesByRule = new Dictionary<IfcClassMapper.Rule, List<ElementType>>();
        var notOwned = new HashSet<ElementId>();

        if (mappingWindow.Applied)
        {
            // Match + ownership request both happen before the transaction
            // opens — WorksharingUtils.CheckoutElements is a round-trip
            // against the central model's ownership bookkeeping, not a
            // local edit, and calling it from inside an open transaction
            // produced consistently wrong results when tested for real
            // (every matched type reported as owned by someone else, even
            // on a solo, detached-from-central file).
            matchesByRule = IfcClassMapper.Match(doc, mappingWindow.Rules);
            notOwned = IfcClassMapper.RequestOwnership(doc, matchesByRule);

            using var mappingTransaction = new Transaction(doc, "BIMFlow: Apply IFC Class Mapping");
            mappingTransaction.Start();
            try
            {
                var results = IfcClassMapper.Apply(matchesByRule, notOwned);
                mappingTransaction.Commit();

                var unmatched = results.Where(r => r.Value.Updated == 0 && r.Value.SkippedNotOwned == 0).Select(r => r.Key.NameContains).ToList();
                var skippedTotal = results.Values.Sum(r => r.SkippedNotOwned);
                var mappingSummary = $"Set \"Export Type to IFC As\" on {results.Values.Sum(r => r.Updated)} type(s) across {results.Count(r => r.Value.Updated > 0)} rule(s).";
                if (skippedTotal > 0)
                    mappingSummary += $"\n\n{skippedTotal} matching type(s) are checked out by another user right now and were skipped — re-run once they're synced and relinquished.";
                if (unmatched.Count > 0)
                    mappingSummary += $"\n\nNo matching family/type name found for: {string.Join(", ", unmatched)}";
                TaskDialog.Show("BIMFlow — IFCExportQA", mappingSummary);
            }
            catch (Exception ex)
            {
                mappingTransaction.RollBack();
                TaskDialog.Show("BIMFlow — IFCExportQA", $"Couldn't apply IFC class mapping: {ex.Message}");
                return Result.Failed;
            }
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

        var summary = $"Exported to:\n{saveDialog.FileName}";

        // Rewrite entity class keywords in the file Revit just wrote — the
        // built-in "Export Type to IFC As" parameter set earlier had no
        // effect on this project's exports even when set by hand in
        // Revit's own UI, so this is the mechanism that actually works.
        if (mappingWindow.Applied && matchesByRule.Count > 0)
        {
            var guidToClass = IfcFilePostProcessor.BuildGuidToClassMap(doc, matchesByRule, notOwned);
            var rewrittenCount = IfcFilePostProcessor.RewriteClasses(saveDialog.FileName, guidToClass);
            summary += $"\n\nRewrote {rewrittenCount} of {guidToClass.Count} mapped element(s) to their target IFC class directly in the file.";
        }

        TaskDialog.Show("BIMFlow — IFCExportQA", summary);
        return Result.Succeeded;
    }
}
