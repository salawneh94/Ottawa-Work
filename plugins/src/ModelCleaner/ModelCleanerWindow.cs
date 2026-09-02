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
using Cursors = System.Windows.Input.Cursors;
using DispatcherPriority = System.Windows.Threading.DispatcherPriority;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.ModelCleaner;

public enum ModelCleanerAction { None, Select, Delete }

/// <summary>
/// Seven independent audits (ModelCleanerEngine) shown as tabs, each with a
/// searchable checklist, All/None/Invert, and three actions: Select in
/// Model (closes and hands off IDs), Show Blast Radius (a read-only query —
/// no transaction, so answered directly in this window), and Delete
/// Selected (closes and hands off IDs for Command.cs to delete inside a
/// transaction — this window never writes to the model itself).
/// </summary>
public class ModelCleanerWindow : OttawaWorkWindow
{
    private static readonly (ModelCleanerCategory Category, string Label)[] Tabs =
    {
        (ModelCleanerCategory.InPlace, "In-Place"),
        (ModelCleanerCategory.UnplacedViews, "Unplaced Views"),
        (ModelCleanerCategory.TemplatesAndFilters, "Templates & Filters"),
        (ModelCleanerCategory.Duplicates, "Duplicates"),
        (ModelCleanerCategory.RogueLinks, "Rogue Links"),
        (ModelCleanerCategory.Materials, "Materials"),
        (ModelCleanerCategory.SheetsAndSchedules, "Sheets & Schedules"),
    };

    private readonly Document _doc;
    // Each category is scanned at most once per window session, lazily, the first time its tab is
    // opened — see GetOrScan and ModelCleanerEngine.ScanCategory's doc comment for why (an eager scan of
    // all seven up front, including the Materials scan's full-document parameter walk, crashed Revit
    // outright on a large real project before this window ever got to render).
    private readonly Dictionary<ModelCleanerCategory, List<ModelCleanerFinding>> _scanned = new();
    private ModelCleanerCategory _activeTab = ModelCleanerCategory.InPlace;

    private readonly StackPanel _tabRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
    private readonly Dictionary<ModelCleanerCategory, Button> _tabButtons = new();

    private readonly StackPanel _statRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
    private readonly TextBlock _summaryBanner = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Warning), Margin = new Thickness(0, 10, 0, 4) };

    private readonly TextBox _searchBox = OttawaWorkUi.TextBox();
    private readonly StackPanel _resultsPanel = new();
    // Not OttawaWorkUi.SectionHeader — that returns a composite StackPanel
    // (colored bar + TextBlock), not a bare TextBlock whose .Text can be
    // updated in place per tab switch, so this is a plain field styled to
    // read the same way (small-caps, accent-colored) instead.
    private readonly TextBlock _tabTitle = new()
    {
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent),
        Margin = new Thickness(0, 4, 0, 6),
    };
    private readonly TextBlock _footerText = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 6, 0, 0) };
    private readonly List<(Border RowBorder, ModelCleanerFinding Finding)> _rows = new();
    private readonly HashSet<int> _selectedIndices = new();

    public ModelCleanerAction Action { get; private set; } = ModelCleanerAction.None;
    public List<ElementId> TargetElementIds { get; private set; } = new();

    public ModelCleanerWindow(Document doc) : base("Ottawa Tools — Model Cleaner", minWidth: 940)
    {
        _doc = doc;

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🧹", "Model Cleaner", "Deep purge — in-place families, unplaced views, duplicate styles, rogue links, orphan materials."));

        var columns = new StackPanel { Orientation = Orientation.Horizontal };

        // ---- Left: summary ----
        var leftCol = new StackPanel { Width = 260, Margin = new Thickness(0, 0, 16, 0) };
        leftCol.Children.Add(_summaryBanner);
        leftCol.Children.Add(OttawaWorkUi.SectionHeader("Scan Summary"));
        leftCol.Children.Add(_statRow);

        leftCol.Children.Add(OttawaWorkUi.SectionHeader("Why it matters"));
        leftCol.Children.Add(new TextBlock
        {
            Text = "Every category here is real, unused model overhead: bloated regen/open times, elements that can't be scheduled or reused, broken external references, and clutter that makes the model harder to navigate.",
            FontSize = 11,
            Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14),
        });

        var rescanButton = OttawaWorkUi.SecondaryButton("Rescan");
        rescanButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        rescanButton.Click += (_, _) => Rescan();
        leftCol.Children.Add(rescanButton);

        columns.Children.Add(leftCol);

        // ---- Right: tabs + results ----
        var rightCol = new StackPanel { Width = 620 };

        foreach (var (category, label) in Tabs)
        {
            var button = OttawaWorkUi.SecondaryButton(label);
            button.Margin = new Thickness(0, 0, 6, 0);
            button.Click += (_, _) => SetTab(category);
            _tabRow.Children.Add(button);
            _tabButtons[category] = button;
        }
        rightCol.Children.Add(_tabRow);

        var toolRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var allButton = OttawaWorkUi.SecondaryButton("All");
        var noneButton = OttawaWorkUi.SecondaryButton("None");
        var invertButton = OttawaWorkUi.SecondaryButton("Invert");
        allButton.Margin = new Thickness(0, 0, 6, 0);
        noneButton.Margin = new Thickness(0, 0, 6, 0);
        invertButton.Margin = new Thickness(0, 0, 10, 0);
        allButton.Click += (_, _) => { foreach (var i in Enumerable.Range(0, _rows.Count)) _selectedIndices.Add(i); RefreshRowHighlights(); };
        noneButton.Click += (_, _) => { _selectedIndices.Clear(); RefreshRowHighlights(); };
        invertButton.Click += (_, _) =>
        {
            var all = Enumerable.Range(0, _rows.Count).ToHashSet();
            all.ExceptWith(_selectedIndices);
            _selectedIndices.Clear();
            foreach (var i in all) _selectedIndices.Add(i);
            RefreshRowHighlights();
        };
        _searchBox.Width = 260;
        _searchBox.ToolTip = "Search rows...";
        _searchBox.TextChanged += (_, _) => RefreshResults();
        toolRow.Children.Add(allButton);
        toolRow.Children.Add(noneButton);
        toolRow.Children.Add(invertButton);
        toolRow.Children.Add(_searchBox);
        rightCol.Children.Add(toolRow);

        rightCol.Children.Add(_tabTitle);
        var resultsScroll = new ScrollViewer { MaxHeight = 320, Content = _resultsPanel };
        rightCol.Children.Add(OttawaWorkUi.Card(resultsScroll, padding: 8));
        rightCol.Children.Add(_footerText);

        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        var selectButton = OttawaWorkUi.PrimaryButton("Select in Model");
        var blastButton = OttawaWorkUi.SecondaryButton("Show Blast Radius");
        var deleteButton = OttawaWorkUi.DangerButton("Delete Selected");
        selectButton.Margin = new Thickness(0, 0, 8, 0);
        blastButton.Margin = new Thickness(0, 0, 8, 0);
        selectButton.Click += (_, _) => Finish(ModelCleanerAction.Select);
        blastButton.Click += (_, _) => ShowBlastRadius();
        deleteButton.Click += (_, _) => ConfirmAndFinishDelete();
        actionRow.Children.Add(selectButton);
        actionRow.Children.Add(blastButton);
        actionRow.Children.Add(deleteButton);
        rightCol.Children.Add(actionRow);

        var closeRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var closeButton = OttawaWorkUi.SecondaryButton("Close");
        closeButton.Click += (_, _) => { DialogResult = false; Close(); };
        closeRow.Children.Add(closeButton);
        rightCol.Children.Add(closeRow);

        columns.Children.Add(rightCol);
        root.Children.Add(columns);

        SetContent(root, padding: 24);

        SetTab(ModelCleanerCategory.InPlace);
    }

    /// <summary>Runs (and caches) exactly one category's scan, the first time it's needed — never all
    /// seven at once. Pumps the dispatcher once after posting a "Scanning…" label so that text actually
    /// paints before the scan call below blocks the UI thread (Revit API calls have to stay on this
    /// thread, so this can't be a background Task — Application.Idling isn't usable either since this
    /// window is already modal). Materials in particular can take real time on a large model; every other
    /// category scan is bounded and fast (family/view/filter/link/sheet counts are always small relative
    /// to total model size), so most tab switches will feel instant even without a spinner.</summary>
    private List<ModelCleanerFinding> GetOrScan(ModelCleanerCategory category)
    {
        if (_scanned.TryGetValue(category, out var cached)) return cached;

        var label = Tabs.First(t => t.Category == category).Label;
        _tabTitle.Text = $"{label.ToUpperInvariant()} — SCANNING…";
        _footerText.Text = "Scanning — this can take a while on large models...";
        Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

        var findings = ModelCleanerEngine.ScanCategory(_doc, category);
        _scanned[category] = findings;
        return findings;
    }

    private void Rescan()
    {
        _scanned.Remove(_activeTab);
        GetOrScan(_activeTab);
        RefreshSummary();
        RefreshResults();
    }

    private void RefreshSummary()
    {
        _statRow.Children.Clear();
        var scannedFindings = _scanned.Values.SelectMany(f => f).ToList();
        var total = scannedFindings.Count;
        var inPlaceInstances = scannedFindings.Where(f => f.Category == ModelCleanerCategory.InPlace).Sum(f => f.InstanceCount);
        var unplacedViews = scannedFindings.Count(f => f.Category == ModelCleanerCategory.UnplacedViews);
        var duplicates = scannedFindings.Count(f => f.Category == ModelCleanerCategory.Duplicates);

        void AddTile(string value, string label, System.Windows.Media.Color? color)
        {
            var tile = OttawaWorkUi.StatTile(value, label, color);
            tile.Margin = new Thickness(0, 0, 8, 8);
            tile.Width = 110;
            _statRow.Children.Add(tile);
        }
        AddTile(total.ToString(), "Total Issues", OttawaWorkUi.Warning);
        AddTile(inPlaceInstances.ToString(), "In-Place Inst.", OttawaWorkUi.Danger);
        AddTile(unplacedViews.ToString(), "Unplaced Views", null);
        AddTile(duplicates.ToString(), "Duplicates", null);

        var unscanned = Tabs.Length - _scanned.Count;
        _summaryBanner.Text = unscanned > 0
            ? $"⚠ {total} finding(s) so far — {unscanned} tab(s) not yet scanned"
            : total > 0 ? $"⚠ Light Cleanup — {total} finding(s)" : "✓ Nothing flagged";
        _summaryBanner.Foreground = OttawaWorkUi.BrushOf(total > 0 ? OttawaWorkUi.Warning : OttawaWorkUi.Success);
    }

    private void SetTab(ModelCleanerCategory category)
    {
        _activeTab = category;
        foreach (var (cat, button) in _tabButtons)
            OttawaWorkUi.SetToggleActive(button, cat == category);
        _selectedIndices.Clear();
        GetOrScan(category);
        RefreshSummary();
        RefreshResults();
    }

    private void RefreshResults()
    {
        _resultsPanel.Children.Clear();
        _rows.Clear();

        var label = Tabs.First(t => t.Category == _activeTab).Label;
        _tabTitle.Text = label.ToUpperInvariant();

        var search = _searchBox.Text?.Trim() ?? "";
        var visible = GetOrScan(_activeTab)
            .Where(f => string.IsNullOrEmpty(search) || f.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || f.Category2.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var finding in visible)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Cursor = Cursors.Hand };
            content.Children.Add(new TextBlock { Text = finding.Name, FontSize = 12, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), Width = 220, TextTrimming = TextTrimming.CharacterEllipsis });
            content.Children.Add(new TextBlock { Text = finding.Category2, FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 130, TextTrimming = TextTrimming.CharacterEllipsis });
            content.Children.Add(new TextBlock { Text = finding.InstanceCount > 0 ? finding.InstanceCount.ToString() : "", FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Warning), Width = 40, HorizontalAlignment = HorizontalAlignment.Right });
            content.Children.Add(new TextBlock { Text = finding.Detail, FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 170, TextTrimming = TextTrimming.CharacterEllipsis });

            var rowBorder = OttawaWorkUi.Card(content, padding: 6);
            rowBorder.Margin = new Thickness(0, 2, 0, 2);
            var index = _rows.Count;
            rowBorder.MouseLeftButtonDown += (_, _) => ToggleRow(index);
            _rows.Add((rowBorder, finding));
            _resultsPanel.Children.Add(rowBorder);
        }

        if (visible.Count == 0)
            _resultsPanel.Children.Add(new TextBlock { Text = "Nothing flagged in this category.", FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(4) });

        RefreshRowHighlights();
    }

    private void ToggleRow(int index)
    {
        if (!_selectedIndices.Add(index)) _selectedIndices.Remove(index);
        RefreshRowHighlights();
    }

    private void RefreshRowHighlights()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            var (rowBorder, _) = _rows[i];
            var selected = _selectedIndices.Contains(i);
            rowBorder.Background = OttawaWorkUi.BrushOf(selected ? OttawaWorkUi.AccentSoft : OttawaWorkUi.CardBackground);
            rowBorder.BorderBrush = OttawaWorkUi.BrushOf(selected ? OttawaWorkUi.Accent : OttawaWorkUi.BorderColor);
        }
        _footerText.Text = $"{_selectedIndices.Count} selected of {_rows.Count} shown";
    }

    private List<ModelCleanerFinding> CurrentSelection()
    {
        return _selectedIndices.Count > 0
            ? _selectedIndices.Select(i => _rows[i].Finding).ToList()
            : _rows.Select(r => r.Finding).ToList();
    }

    private void ShowBlastRadius()
    {
        var selection = CurrentSelection();
        if (selection.Count == 0)
        {
            System.Windows.MessageBox.Show("Nothing to check.", "Ottawa Tools — Model Cleaner");
            return;
        }

        var seedIds = selection.SelectMany(f => f.ElementIds).Distinct().ToList();
        var dependents = ModelCleanerEngine.BlastRadius(_doc, seedIds);

        System.Windows.MessageBox.Show(
            $"Deleting {selection.Count} finding(s) ({seedIds.Count} element(s)) would also affect {dependents.Count} other element(s) that depend on them (dimensions, tags, groups, joined geometry, etc.).",
            "Ottawa Tools — Model Cleaner — Blast Radius");
    }

    private void ConfirmAndFinishDelete()
    {
        var selection = CurrentSelection();
        if (selection.Count == 0)
        {
            System.Windows.MessageBox.Show("Nothing selected to delete.", "Ottawa Tools — Model Cleaner");
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"Delete {selection.Count} finding(s)? This can't be undone from this dialog (Ctrl+Z in Revit still works afterward).",
            "Ottawa Tools — Model Cleaner",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        Finish(ModelCleanerAction.Delete);
    }

    private void Finish(ModelCleanerAction action)
    {
        var selection = CurrentSelection();
        if (selection.Count == 0)
        {
            System.Windows.MessageBox.Show("Nothing selected.", "Ottawa Tools — Model Cleaner");
            return;
        }

        Action = action;
        TargetElementIds = selection.SelectMany(f => f.ElementIds).Distinct().ToList();
        DialogResult = true;
        Close();
    }
}
