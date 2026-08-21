using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;
using System.Text.RegularExpressions;

namespace OttawaWork.NamingConventionAudit;

/// <summary>
/// Checks view, sheet, or family type names against a regex pattern you
/// define — a general-purpose naming check that isn't locked to one
/// category or one hardcoded scheme, for whatever your BEP's naming
/// standard actually is.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "namingconventionaudit";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new NamingRuleWindow();
        if (window.ShowDialog() != true || window.Target is null)
            return Result.Cancelled;

        Regex regex;
        try
        {
            regex = new Regex(window.Pattern);
        }
        catch (ArgumentException ex)
        {
            TaskDialog.Show("Ottawa Tools — NamingConventionAudit", $"That's not a valid regex: {ex.Message}");
            return Result.Cancelled;
        }

        var (names, columnLabel) = window.Target switch
        {
            NamingTarget.Views => (CollectViews(doc), "View"),
            NamingTarget.Sheets => (CollectSheets(doc), "Sheet"),
            NamingTarget.FamilyTypes => (CollectFamilyTypes(doc), "Family Type"),
            _ => (new List<(string name, ElementId id)>(), ""),
        };

        if (names.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — NamingConventionAudit", $"No {window.Target} were found in this project.");
            return Result.Succeeded;
        }

        var rows = names
            .Where(n => !regex.IsMatch(n.name))
            .Select(n => new ResultRow(new[] { n.name, "Doesn't match pattern" }, new List<ElementId> { n.id }))
            .ToList();

        if (rows.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — NamingConventionAudit", $"Checked {names.Count} name(s). All match the pattern.");
            return Result.Succeeded;
        }

        var results = new ResultsListForm(
            "Ottawa Tools — NamingConventionAudit Results",
            $"{rows.Count} of {names.Count} {columnLabel.ToLower()} name(s) don't match \"{window.Pattern}\".",
            new[] { columnLabel, "Result" },
            rows,
            actionButtonText: "Select in model");

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }

    private static List<(string name, ElementId id)> CollectViews(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate)
            .Select(v => (v.Name, v.Id))
            .ToList();
    }

    private static List<(string name, ElementId id)> CollectSheets(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .Select(s => (s.Name, s.Id))
            .ToList();
    }

    private static List<(string name, ElementId id)> CollectFamilyTypes(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(ElementType))
            .Cast<ElementType>()
            .Select(t => (t.Name, t.Id))
            .ToList();
    }
}
