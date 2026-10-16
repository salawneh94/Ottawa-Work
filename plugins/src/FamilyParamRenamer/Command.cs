using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.FamilyParamRenamer;

/// <summary>
/// Opens Family Parameter Renamer, then runs the swap after the window closes: families first, then
/// Project Parameters. No transaction is opened on the project here on purpose: EditFamily and LoadFamily
/// both throw when the project document is modifiable — each family edit runs in its own transaction on
/// the family document, and the project step opens its own transaction once the families are done.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "familyparamrenamer";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;

        var window = new FamilyParamRenamerWindow(doc);
        if (window.ShowDialog() != true || !window.RunRequested)
            return Result.Cancelled;

        // Families first, then Project Parameters: each is its own undo step, and the project step's
        // value copy then sees any family that already carries the new parameter.
        var results = window.SelectedFamilyIds.Count == 0
            ? new List<FamilyRenameResult>()
            : FamilyParamRenamerEngine.Run(doc, window.SelectedFamilyIds, window.SharedFilePath, window.Map);
        var projectResults = FamilyParamRenamerEngine.RunProjectParameters(doc, window.SharedFilePath, window.ProjectMap);

        var headline = new List<string>();
        if (results.Count > 0)
            headline.Add($"Swapped {results.Sum(r => r.Swapped)} parameter(s) in {results.Count(r => r.Swapped > 0)} of {results.Count} famil{(results.Count == 1 ? "y" : "ies")}.");
        if (projectResults.Count > 0)
            headline.Add($"Replaced {projectResults.Count(r => r.Removed)} of {projectResults.Count} project parameter(s) ({projectResults.Sum(r => r.Copied)} value(s) copied).");

        var problems = new List<string>();
        var failedFamilies = results.Count(r => r.Failed);
        if (failedFamilies > 0) problems.Add($"{failedFamilies} famil{(failedFamilies == 1 ? "y" : "ies")} couldn't be updated.");
        var keptProject = projectResults.Count(r => !r.Removed);
        if (keptProject > 0) problems.Add($"{keptProject} old project parameter(s) were kept in Project Parameters.");

        var sections = new List<string>();
        if (projectResults.Count > 0)
            sections.Add("PROJECT PARAMETERS\n\n" + string.Join("\n\n", projectResults.Select(r =>
                $"{r.OldName} → {(r.NewName.Length > 0 ? r.NewName : "?")}{(r.Failed ? " — FAILED" : r.Removed ? " — replaced" : " — old one kept")}\n  " + string.Join("\n  ", r.Messages))));
        if (results.Count > 0)
            sections.Add("FAMILIES\n\n" + string.Join("\n\n", results.Select(r =>
                $"{r.FamilyName}{(r.Failed ? " — FAILED" : $" — {r.Swapped} swapped")}\n  " + string.Join("\n  ", r.Messages))));
        var details = string.Join("\n\n────────\n\n", sections);

        new TaskDialog("Ottawa Tools — Family Parameter Renamer")
        {
            MainInstruction = string.Join("\n", headline),
            MainContent = (problems.Count == 0 ? "" : string.Join(" ", problems) + " See details below.\n\n") +
                "Remember to switch schedules, tags and view filters that used the old parameters over to the new ones.",
            ExpandedContent = details.Length > 8000 ? details[..8000] + "\n…" : details,
            CommonButtons = TaskDialogCommonButtons.Close,
        }.Show();

        return Result.Succeeded;
    }
}
