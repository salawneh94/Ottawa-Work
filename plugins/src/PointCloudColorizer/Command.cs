using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.PointClouds;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.PointCloudColorizer;

/// <summary>
/// Applies a distinct override color to every point cloud instance in the
/// active view, so overlapping scans are easy to tell apart. Revit's
/// native per-point intensity/heatmap coloring isn't exposed through the
/// public API, so a uniform tint per instance is the closest safe
/// equivalent — one click toggles it back off.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "pointcloudcolorizer";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var instances = new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(PointCloudInstance))
            .Cast<PointCloudInstance>()
            .ToList();

        if (instances.Count == 0)
        {
            TaskDialog.Show("BIMFlow — PointCloudColorizer", "No point cloud links are visible in the active view.");
            return Result.Succeeded;
        }

        var currentlyOn = instances
            .Select(i => view.GetElementOverrides(i.Id).ProjectionLineColor)
            .Any(c => c.IsValid);

        using var transaction = new Transaction(doc, currentlyOn ? "BIMFlow: Clear Point Cloud Colors" : "BIMFlow: Colorize Point Clouds");
        transaction.Start();
        try
        {
            for (var i = 0; i < instances.Count; i++)
            {
                if (currentlyOn)
                {
                    view.SetElementOverrides(instances[i].Id, new OverrideGraphicSettings());
                    continue;
                }

                var color = ColorPalette.ForIndex(i);
                var overrides = new OverrideGraphicSettings().SetProjectionLineColor(color).SetCutLineColor(color);
                view.SetElementOverrides(instances[i].Id, overrides);
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "BIMFlow — PointCloudColorizer",
            currentlyOn
                ? $"Cleared the color override on {instances.Count} point cloud instance(s)."
                : $"Applied a distinct color to {instances.Count} point cloud instance(s).");

        return Result.Succeeded;
    }
}
