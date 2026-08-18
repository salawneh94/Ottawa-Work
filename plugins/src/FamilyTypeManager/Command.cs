using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.FamilyTypeManager;

/// <summary>
/// Batch-renames family/system types within a chosen category (find/replace,
/// prefix/suffix, regex — same engine as ViewRenamer/GridRenumber), then
/// offers to purge any types left with zero placed instances.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "familytypemanager";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var categoriesByName = new FilteredElementCollector(doc)
            .WhereElementIsElementType()
            .Select(e => e.Category)
            .Where(c => c is not null)
            .GroupBy(c => c!.Name)
            .Select(g => g.First()!)
            .OrderBy(c => c.Name)
            .ToDictionary(c => c.Name, c => c);

        var categoryPicker = new SimplePickerDialog("BIMFlow — FamilyTypeManager", "Category to manage types for:", categoriesByName.Keys.ToList());
        if (categoryPicker.ShowDialog() != true || categoryPicker.SelectedText is null)
            return Result.Cancelled;

        var selectedCategory = categoriesByName[categoryPicker.SelectedText];

        var types = new FilteredElementCollector(doc)
            .OfCategoryId(selectedCategory.Id)
            .WhereElementIsElementType()
            .ToList();

        if (types.Count == 0)
        {
            TaskDialog.Show("BIMFlow — FamilyTypeManager", "No types were found in that category.");
            return Result.Succeeded;
        }

        var renameWindow = new ElementRenamerForm("BIMFlow — Rename Types", types);
        if (renameWindow.ShowDialog() != true)
            return Result.Cancelled;

        var renamePlan = renameWindow.BuildRenamePlan();

        var instanceTypeIds = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Select(e => e.GetTypeId())
            .ToHashSet();
        var unused = types.Where(t => !instanceTypeIds.Contains(t.Id)).ToList();

        using var transaction = new Transaction(doc, "BIMFlow: Manage Family Types");
        transaction.Start();
        try
        {
            TwoPassRenamer.Apply(
                renamePlan.Select(p => ((Action<string>)(name => p.Element.Name = name), p.NewName)).ToList());
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        var summary = $"Renamed {renamePlan.Count} type(s).";

        if (unused.Count > 0)
        {
            var confirm = TaskDialog.Show(
                "BIMFlow — FamilyTypeManager",
                $"{summary}\n\nFound {unused.Count} type(s) in this category with no placed instances. Delete them?",
                TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

            if (confirm == TaskDialogResult.Yes)
            {
                using var purgeTransaction = new Transaction(doc, "BIMFlow: Purge Unused Types");
                purgeTransaction.Start();
                var deleted = 0;
                foreach (var type in unused)
                {
                    try
                    {
                        doc.Delete(type.Id);
                        deleted++;
                    }
                    catch (Exception)
                    {
                        // Still referenced somewhere purge-unused-style logic can't see (e.g. a nested default type) — skip it.
                    }
                }
                purgeTransaction.Commit();
                summary += $" Deleted {deleted} unused type(s).";
            }
        }

        TaskDialog.Show("BIMFlow — FamilyTypeManager", summary);
        return Result.Succeeded;
    }
}
