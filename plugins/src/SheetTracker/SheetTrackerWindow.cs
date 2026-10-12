using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using FontWeights = System.Windows.FontWeights;
using TextTrimming = System.Windows.TextTrimming;
using TextWrapping = System.Windows.TextWrapping;
using Visibility = System.Windows.Visibility;
using Cursors = System.Windows.Input.Cursors;
using WpfColor = System.Windows.Media.Color;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.SheetTracker;

/// <summary>
/// Every sheet in the project with its issue status — read straight from Revit's own Revisions table
/// (a sheet counts as issued when a revision on it has "Issued" ticked), not from any custom parameter.
/// Filter by status, search, click a sheet to see its full revision history, export the whole list to
/// Excel, or close and jump straight to a sheet. Read-only: nothing here writes to the model.
/// </summary>
public class SheetTrackerWindow : OttawaWorkWindow
{
    private static readonly string[] Filters = { "All", "Issued", "Previously issued", "Revision not issued", "Never issued" };

    private readonly Document _doc;
    private readonly List<SheetTrackerRow> _rows;
    private string _filter = "All";
    private SheetTrackerRow? _selected;

    private readonly Dictionary<string, Button> _filterButtons = new();
    private readonly TextBox _searchBox = OttawaWorkUi.TextBox();
    private readonly StackPanel _listPanel = new();
    private readonly List<(Border Row, SheetTrackerRow Sheet)> _rowViews = new();
    private readonly StackPanel _detailPanel = new();
    private readonly TextBlock _footer = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 6, 0, 0) };

    public ElementId? SheetToOpen { get; private set; }

    public SheetTrackerWindow(Document doc) : base("Ottawa Tools — Sheet Tracker", minWidth: 980)
    {
        _doc = doc;
        _rows = SheetTrackerEngine.Scan(doc);

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🗒️", "Sheet Tracker",
            "Every sheet in the project and whether it has been issued — read from Revit's own Revisions table (a revision with \"Issued\" ticked).", Close));

        var stats = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        void Tile(string value, string label, WpfColor? color)
        {
            var tile = OttawaWorkUi.StatTile(value, label, color);
            tile.Margin = new Thickness(0, 0, 8, 0);
            tile.MinWidth = 120;
            stats.Children.Add(tile);
        }
        Tile(_rows.Count.ToString(), "Sheets", null);
        Tile(_rows.Count(r => r.CurrentIssued).ToString(), "Issued", OttawaWorkUi.Success);
        Tile(_rows.Count(r => !r.CurrentIssued && r.EverIssued).ToString(), "Previously issued", OttawaWorkUi.Warning);
        Tile(_rows.Count(r => !r.EverIssued).ToString(), "Never issued", OttawaWorkUi.Danger);
        root.Children.Add(stats);

        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var filter in Filters)
        {
            var button = OttawaWorkUi.SecondaryButton(filter);
            button.Margin = new Thickness(0, 0, 6, 0);
            button.Click += (_, _) => SetFilter(filter);
            filterRow.Children.Add(button);
            _filterButtons[filter] = button;
        }
        root.Children.Add(filterRow);

        _searchBox.ToolTip = "Search sheet number or name...";
        _searchBox.TextChanged += (_, _) => ApplyVisibility();
        root.Children.Add(_searchBox);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 8, 4) };
        foreach (var (text, width) in Columns)
            header.Children.Add(new TextBlock { Text = text, Width = width, FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary) });
        root.Children.Add(header);

        root.Children.Add(OttawaWorkUi.Card(new ScrollViewer { MaxHeight = 360, Content = _listPanel }, padding: 6));
        root.Children.Add(_footer);

        root.Children.Add(OttawaWorkUi.SectionHeader("Revision history"));
        root.Children.Add(OttawaWorkUi.Card(_detailPanel, padding: 10));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var export = OttawaWorkUi.SecondaryButton("Export to Excel");
        export.Margin = new Thickness(0, 0, 8, 0);
        export.Click += (_, _) => ExportVisible();
        var open = OttawaWorkUi.PrimaryButton("Open Sheet");
        open.Margin = new Thickness(0, 0, 8, 0);
        open.Click += (_, _) => OpenSelected();
        var close = OttawaWorkUi.SecondaryButton("Close");
        close.Click += (_, _) => { DialogResult = false; Close(); };
        actions.Children.Add(export);
        actions.Children.Add(open);
        actions.Children.Add(close);
        root.Children.Add(actions);

        SetContent(root, padding: 24);

        BuildRows();
        SetFilter("All");
        ShowDetail(null);
    }

    private static readonly (string Text, double Width)[] Columns =
    {
        ("NUMBER", 100), ("NAME", 280), ("STATUS", 140), ("REV", 50), ("ISSUE DATE", 100), ("ISSUED TO", 150),
    };

    private static (string Text, WpfColor Color) StatusStyle(SheetTrackerRow row) => SheetTrackerEngine.Status(row) switch
    {
        "Issued" => ("Issued", OttawaWorkUi.Success),
        "Previously issued" => ("Previously issued", OttawaWorkUi.Warning),
        "Revision not issued" => ("Revision not issued", OttawaWorkUi.Warning),
        _ => ("Never issued", OttawaWorkUi.Danger),
    };

    private void BuildRows()
    {
        foreach (var sheet in _rows)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Cursor = Cursors.Hand };
            var (statusText, statusColor) = StatusStyle(sheet);
            line.Children.Add(Cell(sheet.SheetNumber, Columns[0].Width, OttawaWorkUi.Accent, FontWeights.SemiBold));
            line.Children.Add(Cell(sheet.SheetName, Columns[1].Width, OttawaWorkUi.TextPrimary));
            var badgeHost = new StackPanel { Width = Columns[2].Width };
            var badge = OttawaWorkUi.Badge(statusText, statusColor);
            badge.HorizontalAlignment = HorizontalAlignment.Left;
            badgeHost.Children.Add(badge);
            line.Children.Add(badgeHost);
            line.Children.Add(Cell(sheet.CurrentRevision, Columns[3].Width, OttawaWorkUi.TextSecondary));
            line.Children.Add(Cell(sheet.IssueDate, Columns[4].Width, OttawaWorkUi.TextSecondary));
            line.Children.Add(Cell(sheet.IssuedTo, Columns[5].Width, OttawaWorkUi.TextSecondary));

            var row = OttawaWorkUi.Card(line, padding: 6);
            row.Margin = new Thickness(0, 2, 0, 2);
            row.MouseLeftButtonDown += (_, _) => ShowDetail(sheet);
            _rowViews.Add((row, sheet));
            _listPanel.Children.Add(row);
        }
    }

    private static TextBlock Cell(string text, double width, WpfColor color, System.Windows.FontWeight? weight = null) => new()
    {
        Text = text,
        Width = width,
        FontSize = 12,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = OttawaWorkUi.BrushOf(color),
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = System.Windows.VerticalAlignment.Center,
        ToolTip = string.IsNullOrEmpty(text) ? null : text,
    };

    private void SetFilter(string filter)
    {
        _filter = filter;
        foreach (var (name, button) in _filterButtons)
            OttawaWorkUi.SetToggleActive(button, name == filter);
        ApplyVisibility();
    }

    private bool Matches(SheetTrackerRow sheet)
    {
        if (_filter != "All" && SheetTrackerEngine.Status(sheet) != _filter) return false;
        var search = _searchBox.Text?.Trim() ?? "";
        return search.Length == 0
            || sheet.SheetNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
            || sheet.SheetName.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyVisibility()
    {
        var shown = 0;
        foreach (var (row, sheet) in _rowViews)
        {
            var visible = Matches(sheet);
            row.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (visible) shown++;
        }
        _footer.Text = $"{shown} of {_rows.Count} sheet(s) shown";
    }

    private void ShowDetail(SheetTrackerRow? sheet)
    {
        _selected = sheet;
        foreach (var (row, rowSheet) in _rowViews)
        {
            var isSelected = ReferenceEquals(rowSheet, sheet);
            row.Background = OttawaWorkUi.BrushOf(isSelected ? OttawaWorkUi.AccentSoft : OttawaWorkUi.CardBackground);
            row.BorderBrush = OttawaWorkUi.BrushOf(isSelected ? OttawaWorkUi.Accent : OttawaWorkUi.BorderColor);
        }

        _detailPanel.Children.Clear();
        if (sheet is null)
        {
            _detailPanel.Children.Add(Note("Click a sheet above to see every revision on it and whether each one was issued."));
            return;
        }

        _detailPanel.Children.Add(new TextBlock
        {
            Text = $"{sheet.SheetNumber} — {sheet.SheetName}",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary),
            Margin = new Thickness(0, 0, 0, 6),
        });

        if (!string.IsNullOrEmpty(sheet.CreatedBy) || !string.IsNullOrEmpty(sheet.LastChangedBy))
            _detailPanel.Children.Add(Note($"Created by: {Or(sheet.CreatedBy)}   ·   Last changed by: {Or(sheet.LastChangedBy)}"));

        if (sheet.History.Count == 0)
        {
            _detailPanel.Children.Add(Note("No revisions on this sheet — it has never been issued through Revit's revision system."));
            return;
        }

        foreach (var entry in sheet.History)
        {
            var issued = entry.Issued
                ? $"Issued{(string.IsNullOrEmpty(entry.Date) ? "" : " " + entry.Date)}{(string.IsNullOrEmpty(entry.IssuedTo) ? "" : " to " + entry.IssuedTo)}{(string.IsNullOrEmpty(entry.IssuedBy) ? "" : " by " + entry.IssuedBy)}"
                : $"Not issued{(string.IsNullOrEmpty(entry.Date) ? "" : " (dated " + entry.Date + ")")}";
            var text = $"Rev {Or(entry.Number)}  ·  {issued}{(string.IsNullOrEmpty(entry.Description) ? "" : "  ·  " + entry.Description)}";
            _detailPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = OttawaWorkUi.BrushOf(entry.Issued ? OttawaWorkUi.Success : OttawaWorkUi.TextSecondary),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2),
            });
        }
    }

    private static string Or(string value) => string.IsNullOrEmpty(value) ? "—" : value;

    private static TextBlock Note(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 4),
    };

    private void ExportVisible()
    {
        var visible = _rowViews.Where(v => v.Row.Visibility == Visibility.Visible).Select(v => v.Sheet).ToList();
        if (visible.Count == 0)
        {
            System.Windows.MessageBox.Show("No sheets shown to export.", "Ottawa Tools — Sheet Tracker");
            return;
        }

        var (headers, rows) = SheetTrackerEngine.BuildExportTable(_doc, visible);
        var path = BrandedXlsx.Save(
            "Export sheet list",
            $"{_doc.Title}_Sheets.xlsx",
            "Sheet Tracker",
            $"{visible.Count} sheet(s) — filter: {_filter}",
            headers,
            rows);

        if (path is not null)
            System.Windows.MessageBox.Show($"Exported to:\n{path}", "Ottawa Tools — Sheet Tracker");
    }

    private void OpenSelected()
    {
        if (_selected is null)
        {
            System.Windows.MessageBox.Show("Click a sheet first.", "Ottawa Tools — Sheet Tracker");
            return;
        }
        SheetToOpen = _selected.SheetId;
        DialogResult = true;
        Close();
    }
}
