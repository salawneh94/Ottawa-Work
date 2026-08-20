using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

using BIMFlow.Shared;

namespace BIMFlow.PointCloudHeatmap;

/// <summary>Read-only results view for a completed heatmap run — summary counts plus a per-wall breakdown.</summary>
public class PointCloudHeatmapResultsWindow : BimFlowWindow
{
    public PointCloudHeatmapResultsWindow(List<WallHeatmapResult> results) : base("BIMFlow — Point Cloud Heatmap", minWidth: 520)
    {
        var ok = results.Count(r => r.Status == HeatmapStatus.Ok);
        var monitor = results.Count(r => r.Status == HeatmapStatus.Monitor);
        var review = results.Count(r => r.Status == HeatmapStatus.Review);
        var noCoverage = results.Count(r => r.Status == HeatmapStatus.NoCoverage);

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🌡️", "Point Cloud Heatmap", $"{results.Count} wall(s) analysed against scan data."));

        var statsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        statsRow.Children.Add(Spaced(BimFlowUi.StatTile(ok.ToString(), "WITHIN TOLERANCE", BimFlowUi.Success)));
        statsRow.Children.Add(Spaced(BimFlowUi.StatTile(monitor.ToString(), "MONITOR", BimFlowUi.Warning)));
        statsRow.Children.Add(Spaced(BimFlowUi.StatTile(review.ToString(), "REVIEW", BimFlowUi.Danger)));
        if (noCoverage > 0)
            statsRow.Children.Add(Spaced(BimFlowUi.StatTile(noCoverage.ToString(), "NO SCAN COVERAGE", BimFlowUi.TextSecondary)));
        root.Children.Add(statsRow);

        root.Children.Add(BimFlowUi.SectionHeader("Walls"));
        var rowsStack = new StackPanel();
        foreach (var result in results.OrderByDescending(r => r.DeviationPercent))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            row.Children.Add(new TextBlock { Text = result.WallName, FontSize = 12, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextPrimary), Width = 220 });
            row.Children.Add(new TextBlock { Text = $"{result.PointCount} pts", FontSize = 11, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Width = 70 });

            var (label, color) = result.Status switch
            {
                HeatmapStatus.Ok => ($"{result.DeviationPercent:0.0}% — OK", BimFlowUi.Success),
                HeatmapStatus.Monitor => ($"{result.DeviationPercent:0.0}% — Monitor", BimFlowUi.Warning),
                HeatmapStatus.Review => ($"{result.DeviationPercent:0.0}% — Review", BimFlowUi.Danger),
                _ => ("No scan coverage", BimFlowUi.TextSecondary),
            };
            row.Children.Add(BimFlowUi.Badge(label, color));
            rowsStack.Children.Add(row);
        }
        var scroll = new ScrollViewer { MaxHeight = 320, Content = rowsStack };
        root.Children.Add(BimFlowUi.Card(scroll, padding: 8));

        var legendStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        legendStack.Children.Add(new TextBlock { Text = "≤35% deviation: within tolerance, no action needed  ·  35–65%: monitor  ·  >65%: review required", FontSize = 10, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), TextWrapping = System.Windows.TextWrapping.Wrap });
        root.Children.Add(legendStack);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var closeButton = BimFlowUi.PrimaryButton("Close");
        closeButton.Click += (_, _) => { DialogResult = true; Close(); };
        buttonRow.Children.Add(closeButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private static System.Windows.UIElement Spaced(System.Windows.UIElement element)
    {
        if (element is System.Windows.FrameworkElement fe) fe.Margin = new Thickness(0, 0, 10, 0);
        return element;
    }
}
