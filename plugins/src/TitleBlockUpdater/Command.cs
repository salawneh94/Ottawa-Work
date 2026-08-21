using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.TitleBlockUpdater;

/// <summary>
/// Bulk-sets one title block parameter (project info, logo text, address,
/// etc.) across every title block instance in the current project. Scoped
/// to a single open document — updating multiple project files in one run
/// would need Revit to open each file in the background, which is a much
/// larger and riskier operation to automate confidently.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "titleblockupdater";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var instances = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsNotElementType()
            .ToList();

        if (instances.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — TitleBlockUpdater", "No title block instances were found in this project.");
            return Result.Succeeded;
        }

        var editableParamNames = instances[0].Parameters
            .Cast<Parameter>()
            .Where(p => !p.IsReadOnly && p.StorageType == StorageType.String)
            .Select(p => p.Definition.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        if (editableParamNames.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — TitleBlockUpdater", "No editable text parameters were found on this title block.");
            return Result.Succeeded;
        }

        var window = new TitleBlockUpdaterWindow(editableParamNames, instances.Count);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedParameterName is null)
            return Result.Succeeded;

        var updated = 0;

        using var transaction = new Transaction(doc, "Ottawa Tools: Update Title Blocks");
        transaction.Start();
        try
        {
            foreach (var instance in instances)
            {
                var param = instance.LookupParameter(window.SelectedParameterName);
                if (param is not { IsReadOnly: false }) continue;

                param.Set(window.NewValue);
                updated++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("Ottawa Tools — TitleBlockUpdater", $"Updated \"{window.SelectedParameterName}\" on {updated} title block(s).");
        return Result.Succeeded;
    }
}
