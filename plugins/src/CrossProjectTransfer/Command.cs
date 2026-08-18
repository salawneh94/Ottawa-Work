using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.CrossProjectTransfer;

/// <summary>
/// Copies element types (wall types, door types, any category with types)
/// from another currently-open Revit project into this one, category by
/// category with a multi-select picker. Requires both projects open in the
/// same Revit session — it copies from live open documents, not saved
/// files on disk.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "crossprojecttransfer";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var application = commandData.Application.Application;

        var otherDocs = application.Documents
            .Cast<Document>()
            .Where(d => !ReferenceEquals(d, doc) && !d.IsLinked)
            .ToList();

        if (otherDocs.Count == 0)
        {
            TaskDialog.Show("BIMFlow — CrossProjectTransfer", "No other projects are open. Open the source project first, then run this again.");
            return Result.Succeeded;
        }

        var docPicker = new SimplePickerDialog(
            "BIMFlow — CrossProjectTransfer",
            "Copy types from:",
            otherDocs.Select(d => d.Title).ToList());
        if (docPicker.ShowDialog() != true || docPicker.SelectedText is null)
            return Result.Cancelled;

        var sourceDoc = otherDocs.First(d => d.Title == docPicker.SelectedText);

        var sourceTypes = new FilteredElementCollector(sourceDoc)
            .OfClass(typeof(ElementType))
            .Cast<ElementType>()
            .Where(t => t.Category is not null)
            .ToList();

        var categoryNames = sourceTypes
            .Select(t => t.Category!.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        if (categoryNames.Count == 0)
        {
            TaskDialog.Show("BIMFlow — CrossProjectTransfer", $"\"{sourceDoc.Title}\" has no categorized types to copy.");
            return Result.Succeeded;
        }

        var categoryPicker = new SimplePickerDialog("BIMFlow — CrossProjectTransfer", "Category:", categoryNames);
        if (categoryPicker.ShowDialog() != true || categoryPicker.SelectedText is null)
            return Result.Cancelled;

        var typesInCategory = sourceTypes
            .Where(t => t.Category!.Name == categoryPicker.SelectedText)
            .OrderBy(t => t.FamilyName)
            .ThenBy(t => t.Name)
            .ToList();

        var typePicker = new TypeCheckListWindow($"BIMFlow — CrossProjectTransfer: {categoryPicker.SelectedText}", typesInCategory);
        if (typePicker.ShowDialog() != true || typePicker.SelectedTypeIds.Count == 0)
            return Result.Cancelled;

        var copied = 0;
        var failed = new List<string>();

        using var transaction = new Transaction(doc, "BIMFlow: Cross-Project Type Transfer");
        transaction.Start();
        try
        {
            foreach (var typeId in typePicker.SelectedTypeIds)
            {
                var sourceType = sourceDoc.GetElement(typeId) as ElementType;
                try
                {
                    var result = ElementTransformUtils.CopyElements(
                        sourceDoc, new List<ElementId> { typeId }, doc, Transform.Identity, null);
                    if (result.Count > 0) copied++;
                    else failed.Add(sourceType?.Name ?? typeId.ToString());
                }
                catch (Exception)
                {
                    failed.Add(sourceType?.Name ?? typeId.ToString());
                }
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        var summary = $"Copied {copied} of {typePicker.SelectedTypeIds.Count} type(s) from \"{sourceDoc.Title}\".";
        if (failed.Count > 0)
            summary += $"\n\nFailed: {string.Join(", ", failed)}";

        TaskDialog.Show("BIMFlow — CrossProjectTransfer", summary);
        return Result.Succeeded;
    }
}
