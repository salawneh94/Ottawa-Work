using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.ScopeBoxSync;

[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "scopeboxsync";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var scopeBoxes = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_VolumeOfInterest)
            .WhereElementIsNotElementType()
            .ToList();

        if (scopeBoxes.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — Scope Box Sync", "No scope boxes were found in this project.");
            return Result.Succeeded;
        }

        var candidateViews = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate && v.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP) is not null)
            .OrderBy(v => v.Name)
            .ToList();

        if (candidateViews.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — Scope Box Sync", "No views in this project accept a scope box.");
            return Result.Succeeded;
        }

        var window = new ScopeBoxSyncWindow(scopeBoxes, candidateViews);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedScopeBox is null || window.SelectedViews.Count == 0)
            return Result.Succeeded;

        var applied = 0;

        using var transaction = new Transaction(doc, "Ottawa Tools: Apply Scope Box");
        transaction.Start();
        try
        {
            foreach (var view in window.SelectedViews)
            {
                var param = view.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
                if (param is null || param.IsReadOnly) continue;

                param.Set(window.SelectedScopeBox.Id);
                applied++;
            }
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("Ottawa Tools — Scope Box Sync", $"Applied \"{window.SelectedScopeBox.Name}\" to {applied} view(s).");
        return Result.Succeeded;
    }
}
