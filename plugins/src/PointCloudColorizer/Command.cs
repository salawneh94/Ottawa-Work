using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.PointClouds;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.PointCloudColorizer;

/// <summary>
/// Applies a chosen override color (preset swatch or custom) to point
/// cloud instances in the active view — either every one visible, or just
/// the currently-selected ones — with a one-click reset back to default
/// display. Revit's native per-point intensity/heatmap coloring isn't
/// exposed through the public API, so a uniform tint per instance is the
/// closest safe equivalent.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "pointcloudcolorizer";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var selectedIds = uiDoc.Selection.GetElementIds();
        var hasPointCloudSelection = selectedIds.Any(id => doc.GetElement(id) is PointCloudInstance);

        var window = new PointCloudColorWindow(hasPointCloudSelection);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        var allInstances = new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(PointCloudInstance))
            .Cast<PointCloudInstance>()
            .ToList();

        if (allInstances.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — Point Cloud Color", "No point cloud links are visible in the active view.");
            return Result.Succeeded;
        }

        var instances = window.Scope == PointCloudScope.SelectedOnly
            ? allInstances.Where(i => selectedIds.Contains(i.Id)).ToList()
            : allInstances;

        if (instances.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — Point Cloud Color", "No point cloud instances are selected.");
            return Result.Succeeded;
        }

        using var transaction = new Transaction(doc, window.ChosenAction == PointCloudColorAction.ResetToDefault
            ? "Ottawa Tools: Reset Point Cloud Color"
            : "Ottawa Tools: Apply Point Cloud Color");
        transaction.Start();
        try
        {
            foreach (var instance in instances)
            {
                if (window.ChosenAction == PointCloudColorAction.ResetToDefault)
                {
                    view.SetElementOverrides(instance.Id, new OverrideGraphicSettings());
                    continue;
                }

                var overrides = new OverrideGraphicSettings()
                    .SetProjectionLineColor(window.SelectedColor)
                    .SetCutLineColor(window.SelectedColor);
                view.SetElementOverrides(instance.Id, overrides);
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "Ottawa Tools — Point Cloud Color",
            window.ChosenAction == PointCloudColorAction.ResetToDefault
                ? $"Reset {instances.Count} point cloud instance(s) to default display."
                : $"Applied the chosen color to {instances.Count} point cloud instance(s).");

        return Result.Succeeded;
    }
}
