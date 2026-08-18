using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using UniformGrid = System.Windows.Controls.Primitives.UniformGrid;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

using BIMFlow.Shared;

namespace BIMFlow.ModelHealthDashboard;

/// <summary>
/// Latest-run stat tiles + a scrollable run history — dark themed,
/// replacing the old WinForms ListView dialog. Read-only, so there's just
/// a single Close button (no ShowDialog result the caller needs).
/// </summary>
public class HealthDashboardWindow : BimFlowWindow
{
    public HealthDashboardWindow(string docTitle, List<HealthEntry> history) : base($"BIMFlow — Model Health: {docTitle}", minWidth: 460)
    {
        var latest = history.LastOrDefault();
        var previous = history.Count > 1 ? history[^2] : null;

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("💓", "Model Health Dashboard", $"{history.Count} logged run(s) for \"{docTitle}\"."));

        if (latest is not null)
        {
            var tileGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 12) };
            tileGrid.Children.Add(Tile(latest.ElementCount, "Elements", previous?.ElementCount));
            tileGrid.Children.Add(Tile(latest.WarningCount, "Warnings", previous?.WarningCount, lowerIsBetter: true));
            tileGrid.Children.Add(Tile(latest.ViewCount, "Views", previous?.ViewCount));
            tileGrid.Children.Add(Tile(latest.SheetCount, "Sheets", previous?.SheetCount));
            root.Children.Add(tileGrid);
        }

        var historyStack = new StackPanel();
        historyStack.Children.Add(BimFlowUi.SectionHeader("Run history"));
        var rowsStack = new StackPanel();
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 4, 6) };
        foreach (var (text, width) in new[] { ("LOGGED", 150.0), ("ELEMENTS", 80.0), ("WARNINGS", 80.0), ("VIEWS", 60.0), ("SHEETS", 60.0) })
            headerRow.Children.Add(new TextBlock { Text = text, Width = width, FontSize = 10, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary) });
        rowsStack.Children.Add(headerRow);

        foreach (var entry in history.OrderByDescending(e => e.TimestampUtc))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(Cell(entry.TimestampUtc.ToLocalTime().ToString("g"), 150));
            row.Children.Add(Cell(entry.ElementCount.ToString(), 80));
            row.Children.Add(Cell(entry.WarningCount.ToString(), 80));
            row.Children.Add(Cell(entry.ViewCount.ToString(), 60));
            row.Children.Add(Cell(entry.SheetCount.ToString(), 60));
            rowsStack.Children.Add(row);
        }
        var scroll = new ScrollViewer { MaxHeight = 260, Content = rowsStack };
        historyStack.Children.Add(BimFlowUi.Card(scroll, padding: 8));
        root.Children.Add(historyStack);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var closeButton = BimFlowUi.PrimaryButton("Close");
        closeButton.Click += (_, _) => { DialogResult = true; Close(); };
        buttonRow.Children.Add(closeButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private static TextBlock Cell(string text, double width) => new()
    {
        Text = text,
        Width = width,
        FontSize = 12,
        Foreground = BimFlowUi.BrushOf(BimFlowUi.TextPrimary),
    };

    private static System.Windows.UIElement Tile(int value, string label, int? previousValue, bool lowerIsBetter = false)
    {
        if (previousValue is null) return BimFlowUi.StatTile(value.ToString(), label);

        var delta = value - previousValue.Value;
        var improved = lowerIsBetter ? delta <= 0 : delta >= 0;
        var color = delta == 0 ? BimFlowUi.TextPrimary : improved ? BimFlowUi.Success : BimFlowUi.Danger;
        var deltaText = delta switch { > 0 => $"+{delta}", < 0 => delta.ToString(), _ => "±0" };
        return BimFlowUi.StatTile(value.ToString(), $"{label} ({deltaText})", color);
    }
}
