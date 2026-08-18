using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.FamilyAuditor;

/// <summary>
/// Opens every loadable family already in the project (in memory, via
/// Document.EditFamily) and reports nesting depth and parameter count for
/// each, then closes without saving. Scanning family files on disk before
/// they're loaded, or flagging "hardcoded materials" specifically, needs
/// deeper family-editor introspection than this pass covers.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "familyauditor";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var families = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(f => f.IsEditable)
            .OrderBy(f => f.Name)
            .ToList();

        if (families.Count == 0)
        {
            TaskDialog.Show("BIMFlow — FamilyAuditor", "No editable loaded families were found in this project.");
            return Result.Succeeded;
        }

        var confirm = TaskDialog.Show(
            "BIMFlow — FamilyAuditor",
            $"This opens {families.Count} loaded family file(s) in the background to inspect them, then closes each " +
            "without saving. It can take a while on large projects. Continue?",
            TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

        if (confirm != TaskDialogResult.Yes) return Result.Cancelled;

        var rows = new List<ResultRow>();
        var failed = 0;

        foreach (var family in families)
        {
            Document? familyDoc = null;
            try
            {
                familyDoc = doc.EditFamily(family);
                var nestedCount = new FilteredElementCollector(familyDoc)
                    .OfClass(typeof(FamilyInstance))
                    .GetElementCount();
                var paramCount = familyDoc.FamilyManager.Parameters.Size;
                var typeCount = familyDoc.FamilyManager.Types.Size;

                var flags = new List<string>();
                if (nestedCount > 5) flags.Add("Heavily nested");
                if (paramCount > 40) flags.Add("Many parameters");

                rows.Add(new ResultRow(
                    new[] { family.Name, nestedCount.ToString(), typeCount.ToString(), paramCount.ToString(), string.Join(", ", flags) },
                    new List<ElementId> { family.Id }));
            }
            catch (Exception)
            {
                failed++;
            }
            finally
            {
                familyDoc?.Close(false);
            }
        }

        var summary = $"Audited {rows.Count} of {families.Count} family file(s).";
        if (failed > 0) summary += $" {failed} couldn't be opened for inspection.";

        var results = new ResultsListForm(
            "BIMFlow — FamilyAuditor Results",
            summary,
            new[] { "Family", "Nested Families", "Types", "Parameters", "Flags" },
            rows,
            actionButtonText: "Select in model");

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }
}
