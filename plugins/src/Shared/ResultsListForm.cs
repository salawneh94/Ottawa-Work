using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using CornerRadius = System.Windows.CornerRadius;
using FontWeights = System.Windows.FontWeights;
using TextTrimming = System.Windows.TextTrimming;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Cursors = System.Windows.Input.Cursors;

using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

public record ResultRow(string[] Cells, List<ElementId> ElementIds);

/// <summary>
/// Generic "here's what we found" table with a click-to-select-rows action,
/// reused across 17 QA/reporting plugins. Dark themed — replaces the old
/// WinForms multi-select ListView with a scrollable list of clickable rows
/// (WPF has ListView/GridView, but a plain row list is far less risky to
/// blind-author correctly than data-bound columns).
/// </summary>
public class ResultsListForm : OttawaWorkWindow
{
    private readonly List<(Border RowBorder, ResultRow Row)> _rows = new();
    private readonly HashSet<int> _selectedIndices = new();

    public List<ElementId> ElementsToSelect { get; private set; } = new();

    public ResultsListForm(
        string title,
        string summary,
        string[] columnHeaders,
        List<ResultRow> rows,
        string actionButtonText = "Select in model") : base(title, minWidth: 560)
    {
        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("📋", title, summary, Close));

        var columnWidth = Math.Max(100, 520.0 / Math.Max(columnHeaders.Length, 1));

        var listStack = new StackPanel();

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 4, 6) };
        foreach (var header in columnHeaders)
        {
            headerRow.Children.Add(new TextBlock
            {
                Text = header.ToUpperInvariant(),
                Width = columnWidth,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        listStack.Children.Add(headerRow);

        var rowsStack = new StackPanel();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowContent = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var cell in row.Cells)
            {
                rowContent.Children.Add(new TextBlock
                {
                    Text = cell,
                    Width = columnWidth,
                    FontSize = 12,
                    Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 0, 8, 0),
                });
            }

            var rowBorder = new Border
            {
                Background = OttawaWorkUi.BrushOf(OttawaWorkUi.CardBackgroundAlt),
                BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.BorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                Child = rowContent,
            };

            var index = i;
            rowBorder.MouseLeftButtonDown += (_, _) => ToggleRow(index);
            rowsStack.Children.Add(rowBorder);
            _rows.Add((rowBorder, row));
        }
        var scroll = new ScrollViewer { MaxHeight = 380, Content = rowsStack };
        listStack.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));
        root.Children.Add(listStack);

        var hint = new TextBlock
        {
            Text = "Click row(s) to select a subset — leave none selected to act on all.",
            FontSize = 10,
            Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
            Margin = new Thickness(0, 8, 0, 0),
        };
        root.Children.Add(hint);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var closeButton = OttawaWorkUi.SecondaryButton("Close");
        var actionButton = OttawaWorkUi.PrimaryButton(actionButtonText);
        closeButton.Margin = new Thickness(0, 0, 10, 0);
        closeButton.Click += (_, _) => { DialogResult = false; Close(); };
        actionButton.Click += (_, _) =>
        {
            ElementsToSelect = _selectedIndices.Count > 0
                ? _selectedIndices.SelectMany(idx => rows[idx].ElementIds).ToList()
                : rows.SelectMany(r => r.ElementIds).ToList();
            DialogResult = true;
            Close();
        };
        buttonRow.Children.Add(closeButton);
        buttonRow.Children.Add(actionButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void ToggleRow(int index)
    {
        var (rowBorder, _) = _rows[index];
        if (!_selectedIndices.Add(index))
        {
            _selectedIndices.Remove(index);
            rowBorder.Background = OttawaWorkUi.BrushOf(OttawaWorkUi.CardBackgroundAlt);
            rowBorder.BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.BorderColor);
        }
        else
        {
            rowBorder.Background = OttawaWorkUi.BrushOf(OttawaWorkUi.AccentSoft);
            rowBorder.BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent);
        }
    }
}
