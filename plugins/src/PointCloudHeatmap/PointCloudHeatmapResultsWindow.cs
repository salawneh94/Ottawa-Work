using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

using OttawaWork.Shared;

namespace OttawaWork.PointCloudHeatmap;

/// <summary>Read-only results view for a completed heatmap run — summary counts plus a per-wall breakdown.</summary>
public class PointCloudHeatmapResultsWindow : OttawaWorkWindow
{
    public PointCloudHeatmapResultsWindow(List<WallHeatmapResult> results) : base("Ottawa Tools — Point Cloud Heatmap", minWidth: 520)
    {
        var ok = results.Count(r => r.Status == HeatmapStatus.Ok);
        var monitor = results.Count(r => r.Status == HeatmapStatus.Monitor);
        var review = results.Count(r => r.Status == HeatmapStatus.Review);
        var noCoverage = results.Count(r => r.Status == HeatmapStatus.NoCoverage);

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🌡️", "Point Cloud Heatmap", $"{results.Count} wall(s) analysed against scan data."));

        var statsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        statsRow.Children.Add(Spaced(OttawaWorkUi.StatTile(ok.ToString(), "WITHIN TOLERANCE", OttawaWorkUi.Success)));
        statsRow.Children.Add(Spaced(OttawaWorkUi.StatTile(monitor.ToString(), "MONITOR", OttawaWorkUi.Warning)));
        statsRow.Children.Add(Spaced(OttawaWorkUi.StatTile(review.ToString(), "REVIEW", OttawaWorkUi.Danger)));
        if (noCoverage > 0)
            statsRow.Children.Add(Spaced(OttawaWorkUi.StatTile(noCoverage.ToString(), "NO SCAN COVERAGE", OttawaWorkUi.TextSecondary)));
        root.Children.Add(statsRow);

        root.Children.Add(OttawaWorkUi.SectionHeader("Walls"));
        var rowsStack = new StackPanel();
        foreach (var result in results.OrderByDescending(r => r.DeviationPercent))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            row.Children.Add(new TextBlock { Text = result.WallName, FontSize = 12, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), Width = 220 });
            row.Children.Add(new TextBlock { Text = $"{result.PointCount} pts", FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 70 });

            var (label, color) = result.Status switch
            {
                HeatmapStatus.Ok => ($"{result.DeviationPercent:0.0}% — OK", OttawaWorkUi.Success),
                HeatmapStatus.Monitor => ($"{result.DeviationPercent:0.0}% — Monitor", OttawaWorkUi.Warning),
                HeatmapStatus.Review => ($"{result.DeviationPercent:0.0}% — Review", OttawaWorkUi.Danger),
                _ => ("No scan coverage", OttawaWorkUi.TextSecondary),
            };
            row.Children.Add(OttawaWorkUi.Badge(label, color));
            rowsStack.Children.Add(row);
        }
        var scroll = new ScrollViewer { MaxHeight = 320, Content = rowsStack };
        root.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));

        var legendStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        legendStack.Children.Add(new TextBlock { Text = "≤35% deviation: within tolerance, no action needed  ·  35–65%: monitor  ·  >65%: review required", FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), TextWrapping = System.Windows.TextWrapping.Wrap });
        root.Children.Add(legendStack);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var closeButton = OttawaWorkUi.PrimaryButton("Close");
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
