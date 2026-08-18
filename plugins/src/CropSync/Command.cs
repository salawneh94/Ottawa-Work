using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.CropSync;

[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "cropsync";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var sourceView = doc.ActiveView;

        if (sourceView.CropBox is null)
        {
            TaskDialog.Show("BIMFlow — Crop Sync", "The active view doesn't support crop regions.");
            return Result.Cancelled;
        }

        var candidateViews = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate && v.Id != sourceView.Id && v.ViewType == sourceView.ViewType)
            .OrderBy(v => v.Name)
            .ToList();

        if (candidateViews.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Crop Sync", $"No other {sourceView.ViewType} views were found to copy the crop into.");
            return Result.Succeeded;
        }

        var window = new ViewPickerForm(
            "BIMFlow — Crop Sync",
            $"Copy the crop region from \"{sourceView.Name}\" into:",
            candidateViews);

        if (window.ShowDialog() != true)
            return Result.Cancelled;

        var targets = window.SelectedViews;
        if (targets.Count == 0) return Result.Succeeded;

        var copyViewRange = sourceView is ViewPlan && targets.All(v => v is ViewPlan);
        var sourceViewRange = sourceView is ViewPlan sourcePlan ? sourcePlan.GetViewRange() : null;

        using var transaction = new Transaction(doc, "BIMFlow: Copy Crop Region");
        transaction.Start();
        try
        {
            foreach (var target in targets)
            {
                target.CropBoxActive = sourceView.CropBoxActive;
                target.CropBoxVisible = sourceView.CropBoxVisible;
                target.CropBox = CloneBoundingBox(sourceView.CropBox);

                if (copyViewRange && target is ViewPlan targetPlan && sourceViewRange is not null)
                    targetPlan.SetViewRange(sourceViewRange);
            }
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — Crop Sync", $"Copied the crop region to {targets.Count} view(s).");
        return Result.Succeeded;
    }

    private static BoundingBoxXYZ CloneBoundingBox(BoundingBoxXYZ source) => new()
    {
        Transform = source.Transform,
        Min = source.Min,
        Max = source.Max,
    };
}
