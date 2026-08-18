using Autodesk.Revit.Attributes;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.StandardsChecker;

/// <summary>
/// Checks view names or sheet numbers/names against a single approved
/// regex pattern and flags anything that doesn't match. A firm-wide
/// multi-rule profile system is a larger feature (needs profile storage,
/// per-rule types, sharing across a team) — this ships the one-rule-per-run
/// version that's genuinely useful today.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "standardschecker";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new StandardsCheckerWindow();
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        Regex regex;
        try
        {
            regex = new Regex(window.Pattern);
        }
        catch (ArgumentException ex)
        {
            TaskDialog.Show("BIMFlow — StandardsChecker", $"That's not a valid regular expression:\n{ex.Message}");
            return Result.Cancelled;
        }

        var flagged = window.Target switch
        {
            CheckTarget.ViewNames => new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && !regex.IsMatch(v.Name))
                .Select(v => new ResultRow(new[] { v.Name, v.ViewType.ToString() }, new List<ElementId> { v.Id }))
                .ToList(),

            CheckTarget.SheetNumbers => new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !regex.IsMatch(s.SheetNumber))
                .Select(s => new ResultRow(new[] { s.SheetNumber, s.Name }, new List<ElementId> { s.Id }))
                .ToList(),

            CheckTarget.SheetNames => new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !regex.IsMatch(s.Name))
                .Select(s => new ResultRow(new[] { s.SheetNumber, s.Name }, new List<ElementId> { s.Id }))
                .ToList(),

            _ => new List<ResultRow>(),
        };

        if (flagged.Count == 0)
        {
            TaskDialog.Show("BIMFlow — StandardsChecker", "Everything matches the pattern.");
            return Result.Succeeded;
        }

        var columns = window.Target == CheckTarget.ViewNames
            ? new[] { "View Name", "View Type" }
            : new[] { "Sheet Number", "Sheet Name" };

        var results = new ResultsListForm(
            "BIMFlow — StandardsChecker Results",
            $"{flagged.Count} item(s) don't match the pattern.",
            columns,
            flagged);

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
        {
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);
        }

        return Result.Succeeded;
    }
}
