using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.PointClouds;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;

namespace BIMFlow.PointCloudHeatmap;

/// <summary>
/// Compares every wall visible in the active view against nearby point
/// cloud scan data and color-codes each one by how far the scanned
/// surface deviates from the modeled position — green/yellow/red for
/// within-tolerance/monitor/review, matching a fixed 35%/65% threshold.
/// See PointCloudHeatmapAnalyzer for the actual sampling and deviation
/// math, and its known simplifications (door/window openings aren't
/// excluded from the sample).
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "pointcloudheatmap";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var walls = new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(Wall))
            .Cast<Wall>()
            .ToList();

        if (walls.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Point Cloud Heatmap", "No walls are visible in the active view.");
            return Result.Succeeded;
        }

        var choice = MessageBox.Show(
            $"Run heatmap analysis on {walls.Count} wall(s) in the active view against point cloud scan data?\n\nChoose \"No\" to reset previously-applied heatmap colors instead.",
            "BIMFlow — Point Cloud Heatmap",
            MessageBoxButtons.YesNoCancel);

        if (choice == DialogResult.Cancel) return Result.Cancelled;

        if (choice == DialogResult.No)
        {
            using var resetTransaction = new Transaction(doc, "BIMFlow: Reset Point Cloud Heatmap");
            resetTransaction.Start();
            foreach (var wall in walls)
                view.SetElementOverrides(wall.Id, new OverrideGraphicSettings());
            resetTransaction.Commit();

            TaskDialog.Show("BIMFlow — Point Cloud Heatmap", $"Reset heatmap colors on {walls.Count} wall(s).");
            return Result.Succeeded;
        }

        var pointClouds = new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(PointCloudInstance))
            .Cast<PointCloudInstance>()
            .ToList();

        if (pointClouds.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Point Cloud Heatmap", "No point cloud links are visible in the active view.");
            return Result.Succeeded;
        }

        List<WallHeatmapResult> results;
        using (new WaitCursor())
        {
            results = PointCloudHeatmapAnalyzer.Analyze(walls, pointClouds);
        }

        using var transaction = new Transaction(doc, "BIMFlow: Apply Point Cloud Heatmap");
        transaction.Start();
        try
        {
            foreach (var result in results)
            {
                var color = result.Status switch
                {
                    HeatmapStatus.Ok => new Color(40, 167, 69),
                    HeatmapStatus.Monitor => new Color(230, 190, 30),
                    HeatmapStatus.Review => new Color(220, 40, 40),
                    _ => new Color(150, 150, 150),
                };
                var overrides = new OverrideGraphicSettings().SetProjectionLineColor(color).SetCutLineColor(color).SetSurfaceTransparency(30);
                view.SetElementOverrides(result.WallId, overrides);
            }
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        var resultsWindow = new PointCloudHeatmapResultsWindow(results);
        resultsWindow.ShowDialog();

        return Result.Succeeded;
    }

    private sealed class WaitCursor : IDisposable
    {
        private readonly Cursor _previous;
        public WaitCursor() { _previous = Cursor.Current; Cursor.Current = Cursors.WaitCursor; }
        public void Dispose() => Cursor.Current = _previous;
    }
}
