using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using CornerRadius = System.Windows.CornerRadius;
using TextTrimming = System.Windows.TextTrimming;
using Cursors = System.Windows.Input.Cursors;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.WarningResolver;

public record WarningGroup(string Description, FailureSeverity Severity, List<ElementId> ElementIds);

/// <summary>
/// Warning groups as clickable single-select rows — dark themed, replacing
/// the old WinForms single-select ListView.
/// </summary>
public class WarningResultsWindow : BimFlowWindow
{
    private readonly List<(Border RowBorder, WarningGroup Group)> _rows = new();
    private int _selectedIndex = -1;

    public List<ElementId> ElementsToSelect { get; private set; } = new();

    public WarningResultsWindow(List<WarningGroup> groups) : base("BIMFlow — Warning Resolver", minWidth: 520)
    {
        var ordered = groups.OrderByDescending(g => g.ElementIds.Count).ToList();

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("⚠️", "Warning Resolver", $"{groups.Count} distinct warning type(s), {groups.Sum(g => g.ElementIds.Count)} affected element instance(s)."));

        var rowsStack = new StackPanel();
        for (var i = 0; i < ordered.Count; i++)
        {
            var group = ordered[i];
            var severityColor = group.Severity switch
            {
                FailureSeverity.Error => BimFlowUi.Danger,
                FailureSeverity.Warning => BimFlowUi.Warning,
                _ => BimFlowUi.TextSecondary,
            };

            var rowContent = new StackPanel { Orientation = Orientation.Horizontal };
            rowContent.Children.Add(BimFlowUi.Badge(group.Severity.ToString(), severityColor));
            rowContent.Children.Add(new TextBlock
            {
                Text = group.Description,
                Width = 320,
                Margin = new Thickness(10, 0, 10, 0),
                FontSize = 12,
                Foreground = BimFlowUi.BrushOf(BimFlowUi.TextPrimary),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            rowContent.Children.Add(new TextBlock
            {
                Text = $"{group.ElementIds.Count} element(s)",
                FontSize = 11,
                Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary),
            });

            var rowBorder = new Border
            {
                Background = BimFlowUi.BrushOf(BimFlowUi.CardBackgroundAlt),
                BorderBrush = BimFlowUi.BrushOf(BimFlowUi.BorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                Child = rowContent,
            };

            var index = i;
            rowBorder.MouseLeftButtonDown += (_, _) => SelectRow(index);
            rowsStack.Children.Add(rowBorder);
            _rows.Add((rowBorder, group));
        }
        var scroll = new ScrollViewer { MaxHeight = 380, Content = rowsStack };
        root.Children.Add(BimFlowUi.Card(scroll, padding: 8));

        var hint = new TextBlock
        {
            Text = "Click a warning to select its affected element(s) in the model.",
            FontSize = 10,
            Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary),
            Margin = new Thickness(0, 8, 0, 0),
        };
        root.Children.Add(hint);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var closeButton = BimFlowUi.SecondaryButton("Close");
        var selectButton = BimFlowUi.PrimaryButton("Select elements in model");
        closeButton.Margin = new Thickness(0, 0, 10, 0);
        closeButton.Click += (_, _) => { DialogResult = false; Close(); };
        selectButton.Click += (_, _) =>
        {
            if (_selectedIndex >= 0) ElementsToSelect = _rows[_selectedIndex].Group.ElementIds;
            DialogResult = true;
            Close();
        };
        buttonRow.Children.Add(closeButton);
        buttonRow.Children.Add(selectButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void SelectRow(int index)
    {
        if (_selectedIndex >= 0)
        {
            var previous = _rows[_selectedIndex].RowBorder;
            previous.Background = BimFlowUi.BrushOf(BimFlowUi.CardBackgroundAlt);
            previous.BorderBrush = BimFlowUi.BrushOf(BimFlowUi.BorderColor);
        }

        _selectedIndex = index;
        var current = _rows[index].RowBorder;
        current.Background = BimFlowUi.BrushOf(BimFlowUi.AccentSoft);
        current.BorderBrush = BimFlowUi.BrushOf(BimFlowUi.Accent);
    }
}
