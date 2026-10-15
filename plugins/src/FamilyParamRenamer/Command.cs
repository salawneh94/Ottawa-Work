using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.FamilyParamRenamer;

/// <summary>
/// Opens Family Parameter Renamer, then runs the swap after the window closes. No transaction is opened
/// on the project here on purpose: EditFamily and LoadFamily both throw when the project document is
/// modifiable — each family edit runs in its own transaction on the family document instead.
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

        var results = FamilyParamRenamerEngine.Run(doc, window.SelectedFamilyIds, window.SharedFilePath, window.Map);

        var swapped = results.Sum(r => r.Swapped);
        var updated = results.Count(r => r.Swapped > 0);
        var failed = results.Where(r => r.Failed).ToList();

        var details = string.Join("\n\n", results.Select(r => $"{r.FamilyName}{(r.Failed ? " — FAILED" : $" — {r.Swapped} swapped")}\n  " + string.Join("\n  ", r.Messages)));
        new TaskDialog("Ottawa Tools — Family Parameter Renamer")
        {
            MainInstruction = $"Swapped {swapped} parameter(s) in {updated} of {results.Count} famil{(results.Count == 1 ? "y" : "ies")}.",
            MainContent = failed.Count == 0
                ? "Remember to switch schedules, tags and view filters that used the old parameters over to the new ones."
                : $"{failed.Count} famil{(failed.Count == 1 ? "y" : "ies")} couldn't be updated — see details below.",
            ExpandedContent = details.Length > 6000 ? details[..6000] + "\n…" : details,
            CommonButtons = TaskDialogCommonButtons.Close,
        }.Show();

        return Result.Succeeded;
    }
}
