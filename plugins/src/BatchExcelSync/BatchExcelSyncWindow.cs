using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Button = System.Windows.Controls.Button;
using Border = System.Windows.Controls.Border;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Thickness = System.Windows.Thickness;
using CornerRadius = System.Windows.CornerRadius;
using TextWrapping = System.Windows.TextWrapping;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.BatchExcelSync;

/// <summary>
/// The three-column Batch Excel Sync workspace: Scope (which elements),
/// Parameters (which fields, classified instance/type/read-only), and
/// Import &amp; Diff (re-load an edited export, review a live diff against
/// the model, and approve exactly which rows to commit). Export and Browse
/// &amp; Load are pure file I/O — no document transaction needed, so both run
/// directly from this window while it stays open for the next step.
/// Committing actually writes to the model, so that's deferred to
/// Command.cs after this window closes, same as every other Ottawa Tools dialog.
/// </summary>
public class BatchExcelSyncWindow : OttawaWorkWindow
{
    private readonly Document _doc;
    private readonly UIDocument _uiDoc;
    private readonly Dictionary<string, Category> _categoriesByName;

    private readonly Button _instancesButton = OttawaWorkUi.SecondaryButton("Instances");
    private readonly Button _typesButton = OttawaWorkUi.SecondaryButton("Types");
    private readonly ComboBox _categoryBox = OttawaWorkUi.ComboBox();
    private readonly ComboBox _levelBox = OttawaWorkUi.ComboBox();
    private readonly TextBlock _hintText = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _countBadgeText = new() { FontSize = 13, FontWeight = System.Windows.FontWeights.Bold, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary) };

    private readonly TextBox _searchBox = OttawaWorkUi.TextBox();
    private readonly StackPanel _paramListPanel = new();
    private readonly TextBlock _paramReadyText = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 8, 0, 0) };
    private readonly List<(ParamColumn Column, CheckBox Box, Border Container)> _paramRows = new();

    private readonly TextBlock _loadedFileText = new() { FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
    private readonly Button _tabChanges = OttawaWorkUi.SecondaryButton("Changes");
    private readonly Button _tabType = OttawaWorkUi.SecondaryButton("Type");
    private readonly Button _tabConflicts = OttawaWorkUi.SecondaryButton("Conflicts");
    private readonly Button _tabAll = OttawaWorkUi.SecondaryButton("All");
    private readonly StackPanel _diffPanel = new();
    private readonly Button _commitButton = OttawaWorkUi.SuccessButton("Commit approved changes (0)");
    private DiffStatus? _activeFilter;
    private readonly List<(DiffRow Row, CheckBox Box, Border Container)> _diffRowViews = new();

    private SyncScope _scope = SyncScope.Instances;
    private List<Element> _scopeElements = new();

    public List<DiffRow> ApprovedRows { get; private set; } = new();
    public SyncScope CommitScope { get; private set; }
    public List<ParamColumn> CommitColumns { get; private set; } = new();
    public bool Committed { get; private set; }

    public BatchExcelSyncWindow(Document doc, UIDocument uiDoc) : base("Ottawa Tools — Batch Excel Sync", minWidth: 1020)
    {
        _doc = doc;
        _uiDoc = uiDoc;

        _categoriesByName = new Dictionary<string, Category>();
        foreach (var (label, builtIn) in CommonCategories.Roster)
        {
            var category = Category.GetCategory(doc, builtIn);
            if (category is not null) _categoriesByName[label] = category;
        }

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("📊", "Batch Excel Sync", "Step 1: set scope & category. Step 2: pick parameters to export. Step 3: import Excel back to review & commit changes."));

        var columns = new StackPanel { Orientation = Orientation.Horizontal };
        columns.Children.Add(BuildScopeColumn());
        columns.Children.Add(BuildParametersColumn());
        columns.Children.Add(BuildImportColumn());
        root.Children.Add(columns);

        SetContent(root, padding: 24);

        SetScope(SyncScope.Instances);
        RefreshLevels();
        if (_categoryBox.Items.Count > 0) _categoryBox.SelectedIndex = 0;
        RefreshScope();
    }

    private StackPanel BuildScopeColumn()
    {
        var col = new StackPanel { Width = 260, Margin = new Thickness(0, 0, 16, 0) };
        col.Children.Add(OttawaWorkUi.SectionHeader("1  Scope"));

        var toggleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        _instancesButton.Margin = new Thickness(0, 0, 8, 0);
        _instancesButton.Click += (_, _) => SetScope(SyncScope.Instances);
        _typesButton.Click += (_, _) => SetScope(SyncScope.Types);
        toggleRow.Children.Add(_instancesButton);
        toggleRow.Children.Add(_typesButton);
        col.Children.Add(toggleRow);

        col.Children.Add(OttawaWorkUi.SectionHeader("Category"));
        _categoryBox.Items.Add(BatchExcelSyncEngine.CurrentSelectionLabel);
        foreach (var (label, _) in CommonCategories.Roster)
            if (_categoriesByName.ContainsKey(label)) _categoryBox.Items.Add(label);
        _categoryBox.SelectionChanged += (_, _) => { RefreshLevels(); RefreshScope(); };
        col.Children.Add(_categoryBox);

        col.Children.Add(OttawaWorkUi.SectionHeader("Level filter"));
        _levelBox.SelectionChanged += (_, _) => RefreshScope();
        col.Children.Add(_levelBox);

        _hintText.Text = "Select elements in Revit, or choose a category above.";
        col.Children.Add(_hintText);

        col.Children.Add(OttawaWorkUi.Card(_countBadgeText, padding: 10));

        var exportButton = OttawaWorkUi.PrimaryButton("Export to Excel");
        exportButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        exportButton.Margin = new Thickness(0, 16, 0, 0);
        exportButton.Click += (_, _) => ExportToExcel();
        col.Children.Add(exportButton);

        return col;
    }

    private StackPanel BuildParametersColumn()
    {
        var col = new StackPanel { Width = 260, Margin = new Thickness(0, 0, 16, 0) };
        col.Children.Add(OttawaWorkUi.SectionHeader("2  Parameters"));

        _searchBox.ToolTip = "Search parameters...";
        _searchBox.TextChanged += (_, _) => RefreshParamVisibility();
        col.Children.Add(_searchBox);

        var quickRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var allButton = OttawaWorkUi.SecondaryButton("All");
        var writableButton = OttawaWorkUi.SecondaryButton("Writable");
        var noneButton = OttawaWorkUi.SecondaryButton("None");
        allButton.Margin = new Thickness(0, 0, 6, 0);
        writableButton.Margin = new Thickness(0, 0, 6, 0);
        allButton.Click += (_, _) => { foreach (var r in _paramRows.Where(r => !r.Column.IsReadOnly)) r.Box.IsChecked = true; RefreshReadyText(); };
        writableButton.Click += (_, _) => { foreach (var r in _paramRows) r.Box.IsChecked = !r.Column.IsReadOnly; RefreshReadyText(); };
        noneButton.Click += (_, _) => { foreach (var r in _paramRows) r.Box.IsChecked = false; RefreshReadyText(); };
        quickRow.Children.Add(allButton);
        quickRow.Children.Add(writableButton);
        quickRow.Children.Add(noneButton);
        col.Children.Add(quickRow);

        var legendRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        legendRow.Children.Add(Dot(OttawaWorkUi.Success));
        legendRow.Children.Add(new TextBlock { Text = "Instance  ", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary) });
        legendRow.Children.Add(Dot(OttawaWorkUi.Warning));
        legendRow.Children.Add(new TextBlock { Text = "Type  ", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary) });
        legendRow.Children.Add(Dot(OttawaWorkUi.Danger));
        legendRow.Children.Add(new TextBlock { Text = "Read-only", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary) });
        col.Children.Add(legendRow);

        var scroll = new ScrollViewer { MaxHeight = 420, Content = _paramListPanel };
        col.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));
        col.Children.Add(_paramReadyText);

        return col;
    }

    private static Border Dot(System.Windows.Media.Color color) => new()
    {
        Width = 8,
        Height = 8,
        CornerRadius = new CornerRadius(4),
        Margin = new Thickness(0, 0, 4, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Background = OttawaWorkUi.BrushOf(color),
    };

    private StackPanel BuildImportColumn()
    {
        var col = new StackPanel { Width = 480 };
        col.Children.Add(OttawaWorkUi.SectionHeader("3  Import & diff"));

        var browseRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var browseButton = OttawaWorkUi.PrimaryButton("Browse & load");
        browseButton.Click += (_, _) => BrowseAndLoad();
        browseRow.Children.Add(browseButton);
        browseRow.Children.Add(_loadedFileText);
        col.Children.Add(browseRow);

        var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        _tabChanges.Margin = new Thickness(0, 0, 6, 0);
        _tabType.Margin = new Thickness(0, 0, 6, 0);
        _tabConflicts.Margin = new Thickness(0, 0, 6, 0);
        _tabChanges.Click += (_, _) => SetActiveFilter(DiffStatus.Changed);
        _tabType.Click += (_, _) => SetActiveFilter(DiffStatus.TypeChange);
        _tabConflicts.Click += (_, _) => SetActiveFilter(DiffStatus.Conflict);
        _tabAll.Click += (_, _) => SetActiveFilter(null);
        tabRow.Children.Add(_tabChanges);
        tabRow.Children.Add(_tabType);
        tabRow.Children.Add(_tabConflicts);
        tabRow.Children.Add(_tabAll);
        col.Children.Add(tabRow);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 4, 4) };
        headerRow.Children.Add(new TextBlock { Text = "", Width = 24 });
        headerRow.Children.Add(new TextBlock { Text = "ELEMENT", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 120 });
        headerRow.Children.Add(new TextBlock { Text = "PARAMETER", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 90 });
        headerRow.Children.Add(new TextBlock { Text = "CURRENT", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 80 });
        headerRow.Children.Add(new TextBlock { Text = "NEW", FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 80 });
        col.Children.Add(headerRow);

        var scroll = new ScrollViewer { MaxHeight = 360, Content = _diffPanel };
        col.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));

        _commitButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _commitButton.Margin = new Thickness(0, 16, 0, 0);
        _commitButton.Click += (_, _) => FinishCommit();
        col.Children.Add(_commitButton);

        SetActiveFilter(null);

        return col;
    }

    private void SetScope(SyncScope scope)
    {
        _scope = scope;
        OttawaWorkUi.SetToggleActive(_instancesButton, scope == SyncScope.Instances);
        OttawaWorkUi.SetToggleActive(_typesButton, scope == SyncScope.Types);
        _levelBox.IsEnabled = scope == SyncScope.Instances;
        RefreshScope();
    }

    private void RefreshLevels()
    {
        _levelBox.Items.Clear();
        _levelBox.Items.Add("All Levels");
        var levels = new FilteredElementCollector(_doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).Select(l => l.Name);
        foreach (var name in levels) _levelBox.Items.Add(name);
        _levelBox.SelectedIndex = 0;
    }

    private void RefreshScope()
    {
        var categoryLabel = _categoryBox.SelectedItem as string ?? BatchExcelSyncEngine.CurrentSelectionLabel;
        _categoriesByName.TryGetValue(categoryLabel, out var category);

        ElementId? levelId = null;
        if (_levelBox.SelectedItem is string levelName && levelName != "All Levels")
        {
            var level = new FilteredElementCollector(_doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault(l => l.Name == levelName);
            levelId = level?.Id;
        }

        _scopeElements = BatchExcelSyncEngine.CollectScope(_doc, _uiDoc.Selection.GetElementIds(), _scope, categoryLabel, category, levelId);
        _countBadgeText.Text = $"{_scopeElements.Count} element(s)";

        _hintText.Text = categoryLabel == BatchExcelSyncEngine.CurrentSelectionLabel
            ? "Using whatever is currently selected in Revit."
            : $"Every {(_scope == SyncScope.Types ? "type" : "instance")} of {categoryLabel} in the project.";

        RefreshParameters();
    }

    private void RefreshParameters()
    {
        _paramListPanel.Children.Clear();
        _paramRows.Clear();

        var columns = BatchExcelSyncEngine.ClassifyParameters(_doc, _scopeElements, _scope);
        foreach (var column in columns.OrderBy(c => c.IsReadOnly).ThenBy(c => c.IsTypeParam))
        {
            var dotColor = column.IsReadOnly ? OttawaWorkUi.Danger : column.IsTypeParam ? OttawaWorkUi.Warning : OttawaWorkUi.Success;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(Dot(dotColor));
            var label = column.IsTypeParam ? $"{column.Name}  [type]" : column.Name;
            if (column.IsReadOnly) label += "  read-only";
            var box = OttawaWorkUi.CheckBoxItem(label, isChecked: !column.IsReadOnly);
            box.IsEnabled = !column.IsReadOnly;
            box.Foreground = OttawaWorkUi.BrushOf(column.IsReadOnly ? OttawaWorkUi.TextSecondary : OttawaWorkUi.TextPrimary);
            box.Click += (_, _) => RefreshReadyText();
            row.Children.Add(box);

            var container = new Border { Child = row };
            _paramListPanel.Children.Add(container);
            _paramRows.Add((column, box, container));
        }

        RefreshParamVisibility();
        RefreshReadyText();
    }

    private void RefreshParamVisibility()
    {
        var search = _searchBox.Text?.Trim() ?? "";
        foreach (var (column, _, container) in _paramRows)
        {
            container.Visibility = string.IsNullOrEmpty(search) || column.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }
    }

    private void RefreshReadyText()
    {
        var checkedCount = _paramRows.Count(r => r.Box.IsChecked == true);
        _paramReadyText.Text = $"Ready: {_scopeElements.Count} element(s) × {checkedCount} parameter(s)";
    }

    private void ExportToExcel()
    {
        var selectedColumns = _paramRows.Where(r => r.Box.IsChecked == true).Select(r => r.Column).ToList();
        if (_scopeElements.Count == 0 || selectedColumns.Count == 0)
        {
            System.Windows.MessageBox.Show("Select at least one element and one parameter first.", "Ottawa Tools — Batch Excel Sync");
            return;
        }

        var (headers, rows) = BatchExcelSyncEngine.BuildExportTable(_doc, _scopeElements, selectedColumns, _scope);
        var savedPath = BrandedXlsx.Save(
            "Export parameters",
            $"{_doc.Title}_Params.xlsx",
            "Batch Excel Sync",
            $"{_scopeElements.Count} element(s) × {selectedColumns.Count} parameter(s)",
            headers,
            rows);

        if (savedPath is not null)
            System.Windows.MessageBox.Show($"Exported to:\n{savedPath}\n\nEdit it, then use \"Browse & load\" below to review and commit changes.", "Ottawa Tools — Batch Excel Sync");
    }

    private void BrowseAndLoad()
    {
        using var dialog = new OpenFileDialog { Title = "Load edited export", Filter = "Excel Workbook (*.xlsx)|*.xlsx" };
        if (dialog.ShowDialog() != WinFormsDialogResult.OK) return;

        var table = BrandedXlsx.ReadTable(dialog.FileName);
        if (table is null)
        {
            System.Windows.MessageBox.Show("This file doesn't look like a Batch Excel Sync export — no header row was found.", "Ottawa Tools — Batch Excel Sync");
            return;
        }

        var (headers, rows) = table.Value;
        var knownColumns = BatchExcelSyncEngine.ClassifyParameters(_doc, _scopeElements, _scope);
        var diff = BatchExcelSyncEngine.ComputeDiff(_doc, headers, rows, _scope, knownColumns);

        _loadedFileText.Text = $"{System.IO.Path.GetFileName(dialog.FileName)} — {rows.Count} record(s) loaded";
        BuildDiffRows(diff, knownColumns);
    }

    private void BuildDiffRows(List<DiffRow> diff, List<ParamColumn> knownColumns)
    {
        _diffPanel.Children.Clear();
        _diffRowViews.Clear();

        foreach (var row in diff)
        {
            var checkBox = new CheckBox
            {
                IsChecked = row.Status is DiffStatus.Changed or DiffStatus.TypeChange,
                IsEnabled = row.Status != DiffStatus.Conflict,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 24,
            };
            checkBox.Click += (_, _) => RefreshCommitCount();

            var rowContent = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            rowContent.Children.Add(checkBox);
            rowContent.Children.Add(new TextBlock { Text = row.ElementLabel, FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent), Width = 120, TextTrimming = System.Windows.TextTrimming.CharacterEllipsis });
            rowContent.Children.Add(new TextBlock { Text = row.ParamName, FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), Width = 90, TextTrimming = System.Windows.TextTrimming.CharacterEllipsis });
            rowContent.Children.Add(new TextBlock { Text = row.CurrentValue, FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 80, TextTrimming = System.Windows.TextTrimming.CharacterEllipsis });

            var (newValueColor, statusText) = row.Status switch
            {
                DiffStatus.Changed => (OttawaWorkUi.Success, "Changed"),
                DiffStatus.TypeChange => (OttawaWorkUi.Warning, "Type"),
                DiffStatus.Conflict => (OttawaWorkUi.Danger, "Conflict"),
                _ => (OttawaWorkUi.TextSecondary, "Same"),
            };
            rowContent.Children.Add(new TextBlock { Text = row.NewValue, FontSize = 11, Foreground = OttawaWorkUi.BrushOf(newValueColor), FontWeight = row.Status == DiffStatus.Same ? System.Windows.FontWeights.Normal : System.Windows.FontWeights.SemiBold, Width = 80, TextTrimming = System.Windows.TextTrimming.CharacterEllipsis });
            rowContent.Children.Add(OttawaWorkUi.Badge(statusText, newValueColor));

            var container = new Border { Padding = new Thickness(2), Child = rowContent };
            _diffPanel.Children.Add(container);
            _diffRowViews.Add((row, checkBox, container));
        }

        _tabChanges.Content = $"CHANGES ({diff.Count(r => r.Status == DiffStatus.Changed)})";
        _tabType.Content = $"TYPE ({diff.Count(r => r.Status == DiffStatus.TypeChange)})";
        _tabConflicts.Content = $"CONFLICTS ({diff.Count(r => r.Status == DiffStatus.Conflict)})";
        _tabAll.Content = $"ALL ({diff.Count})";

        CommitScope = _scope;
        CommitColumns = knownColumns;

        SetActiveFilter(_activeFilter);
        RefreshCommitCount();
    }

    private void SetActiveFilter(DiffStatus? status)
    {
        _activeFilter = status;
        OttawaWorkUi.SetToggleActive(_tabChanges, status == DiffStatus.Changed);
        OttawaWorkUi.SetToggleActive(_tabType, status == DiffStatus.TypeChange);
        OttawaWorkUi.SetToggleActive(_tabConflicts, status == DiffStatus.Conflict);
        OttawaWorkUi.SetToggleActive(_tabAll, status is null);

        foreach (var (row, _, container) in _diffRowViews)
            container.Visibility = status is null || row.Status == status
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
    }

    private void RefreshCommitCount()
    {
        var count = _diffRowViews.Count(r => r.Box.IsChecked == true);
        _commitButton.Content = $"Commit approved changes ({count})".ToUpperInvariant();
    }

    private void FinishCommit()
    {
        ApprovedRows = _diffRowViews.Where(r => r.Box.IsChecked == true).Select(r => r.Row).ToList();
        if (ApprovedRows.Count == 0)
        {
            System.Windows.MessageBox.Show("Check at least one row to commit.", "Ottawa Tools — Batch Excel Sync");
            return;
        }

        Committed = true;
        DialogResult = true;
        Close();
    }
}
