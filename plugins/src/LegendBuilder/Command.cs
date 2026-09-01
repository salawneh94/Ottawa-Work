using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.LegendBuilder;

/// <summary>
/// Scans a category grouped by a chosen parameter's distinct values (with
/// element counts), lets you assign a color per value (or Auto-assign),
/// style it (title, size, fine-tune spacing, placement, options), preview it
/// live, then generates the actual legend content — filled-region swatches
/// plus text-note labels/counts/title (LegendBuilderEngine). Revit has no
/// API to create a legend view from nothing, so this always needs at least
/// one existing (even blank) legend view to duplicate first — the same
/// constraint the native "New Legend" ribbon command has under the hood,
/// and the same reason the previous version of this tool needed one too.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "legendbuilder";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var legends = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.ViewType == ViewType.Legend && !v.IsTemplate)
            .OrderBy(v => v.Name)
            .ToList();

        if (legends.Count == 0)
        {
            TaskDialog.Show(
                "Ottawa Tools — Legend Builder",
                "No legend views were found to use as a starting point. Create one blank legend view first (View tab → Legends → Legend), then run this again.");
            return Result.Cancelled;
        }

        var picker = new SimplePickerDialog(
            "Ottawa Tools — Legend Builder",
            "Duplicate which legend as the starting point?",
            legends.Select(v => v.Name).ToList());

        if (picker.ShowDialog() != true || picker.SelectedText is null)
            return Result.Cancelled;

        var sourceLegend = legends.First(v => v.Name == picker.SelectedText);

        var window = new LegendBuilderWindow(doc);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        using var transaction = new Transaction(doc, "Ottawa Tools: Build Legend");
        transaction.Start();
        try
        {
            var newLegendId = sourceLegend.Duplicate(ViewDuplicateOption.Duplicate);
            var newLegend = (View)doc.GetElement(newLegendId);
            newLegend.Name = MakeUniqueName(doc, $"{sourceLegend.Name} — {window.SelectedParameterName}");

            LegendBuilderEngine.GenerateLegendContent(doc, newLegend, window.Style, window.Rows);

            transaction.Commit();
            TaskDialog.Show("Ottawa Tools — Legend Builder", $"Built a new legend with {window.Rows.Count} value(s) placed.");
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("Ottawa Tools — Legend Builder", $"Couldn't build the legend: {ex.Message}");
            return Result.Failed;
        }

        return Result.Succeeded;
    }

    private static string MakeUniqueName(Document doc, string baseName)
    {
        var existingNames = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Select(v => v.Name)
            .ToHashSet();

        if (!existingNames.Contains(baseName)) return baseName;

        var i = 2;
        while (existingNames.Contains($"{baseName} {i}")) i++;
        return $"{baseName} {i}";
    }
}
