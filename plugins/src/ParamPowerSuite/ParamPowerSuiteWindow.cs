using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Grid = System.Windows.Controls.Grid;
using ColumnDefinition = System.Windows.Controls.ColumnDefinition;
using Border = System.Windows.Controls.Border;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Thickness = System.Windows.Thickness;
using Visibility = System.Windows.Visibility;
using TextWrapping = System.Windows.TextWrapping;
using GridLength = System.Windows.GridLength;
using GridUnitType = System.Windows.GridUnitType;
using UIElement = System.Windows.UIElement;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.ParamPowerSuite;

/// <summary>
/// A multi-tab parameter workbench, not a one-shot dialog: unlike every
/// other Ottawa Tools window (which computes a pending action and hands it to
/// Command.cs to write inside a single transaction, then closes), this
/// window stays open across many independent Apply clicks — Bulk Set, then
/// maybe Find/Replace, then Combine, all in the same session against the
/// same loaded element scope. That means it opens its own Transaction per
/// Apply click directly (see each tab's partial-class file), rather than
/// deferring every write to Command.cs the way Din276Window/BatchExcelSync
/// do — there's no single "pending action" to defer since the whole point
/// is running several.
///
/// Layout: a fixed-width left sidebar (category/level scope, Load Elements,
/// a grouped family/type list with counts) shared by every tab, a top nav
/// bar of 7 toggle buttons swapping which tab's content is visible, and a
/// bottom status bar showing the running Applied/Skipped/Failed counts from
/// the most recent action.
/// </summary>
public partial class ParamPowerSuiteWindow : OttawaWorkWindow
{
    private static readonly string[] TabNames =
    {
        "Bulk Set", "Find / Replace", "Case Transform", "Copy A → B", "Combine (Tokens)", "Jammer", "Create Param",
    };

    private readonly Document _doc;
    private readonly Dictionary<BuiltInCategory, CheckBox> _scopeCategoryChecks = new();
    private readonly ComboBox _levelBox = OttawaWorkUi.ComboBox();
    private readonly List<ElementId?> _levelIdsByIndex = new();
    private readonly StackPanel _familyTypePanel = new();
    private readonly TextBlock _scopeStatusText = OttawaWorkUi.FieldLabel("No elements loaded yet.");
    private readonly TextBlock _statusText = new() { FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), VerticalAlignment = VerticalAlignment.Center };
    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly Dictionary<string, UIElement> _tabPanels = new();
    private readonly List<ComboBox> _parameterDependentComboBoxes = new();

    private List<Element> _loadedElements = new();

    public ParamPowerSuiteWindow(Document doc) : base("Param Power Suite", minWidth: 1000)
    {
        _doc = doc;

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sidebar = BuildSidebar();
        Grid.SetColumn(sidebar, 0);
        root.Children.Add(sidebar);

        var mainStack = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        mainStack.Children.Add(OttawaWorkUi.TitleBar("🧰", "Param Power Suite", "Bulk-edit parameters across every loaded element: set, find/replace, transform, copy, combine, jam to shared, or create a new bound parameter."));
        mainStack.Children.Add(BuildNavBar());

        var tabHost = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        foreach (var name in TabNames)
        {
            var panel = BuildTabContent(name);
            panel.Visibility = Visibility.Collapsed;
            _tabPanels[name] = panel;
            tabHost.Children.Add(panel);
        }
        mainStack.Children.Add(tabHost);

        mainStack.Children.Add(BuildStatusBar());

        Grid.SetColumn(mainStack, 1);
        root.Children.Add(mainStack);

        SetContent(root, padding: 20);

        RefreshLevelOptions();
        ShowTab(TabNames[0]);
    }

    private UIElement BuildTabContent(string name) => name switch
    {
        "Bulk Set" => BuildBulkSetTab(),
        "Find / Replace" => BuildFindReplaceTab(),
        "Case Transform" => BuildCaseTransformTab(),
        "Copy A → B" => BuildCopyTab(),
        "Combine (Tokens)" => BuildCombineTab(),
        "Jammer" => BuildJammerTab(),
        "Create Param" => BuildCreateParamTab(),
        _ => new StackPanel(),
    };

    private UIElement BuildNavBar()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        foreach (var name in TabNames)
        {
            var button = OttawaWorkUi.SecondaryButton(name);
            button.Margin = new Thickness(0, 0, 8, 0);
            button.Click += (_, _) => ShowTab(name);
            _navButtons[name] = button;
            row.Children.Add(button);
        }
        return row;
    }

    private void ShowTab(string name)
    {
        foreach (var (tabName, panel) in _tabPanels)
            panel.Visibility = tabName == name ? Visibility.Visible : Visibility.Collapsed;

        foreach (var (tabName, button) in _navButtons)
            OttawaWorkUi.SetToggleActive(button, tabName == name);
    }

    // ---------------- Shared left sidebar (element scope) ----------------

    private UIElement BuildSidebar()
    {
        var stack = new StackPanel();

        stack.Children.Add(OttawaWorkUi.SectionHeader("Categories"));
        var toggleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var allButton = OttawaWorkUi.SecondaryButton("All");
        var noneButton = OttawaWorkUi.SecondaryButton("None");
        allButton.Margin = new Thickness(0, 0, 6, 0);
        allButton.Click += (_, _) => SetAllCategories(true);
        noneButton.Click += (_, _) => SetAllCategories(false);
        toggleRow.Children.Add(allButton);
        toggleRow.Children.Add(noneButton);
        stack.Children.Add(toggleRow);

        var categoryScroll = new ScrollViewer { MaxHeight = 220 };
        var categoryList = new StackPanel();
        foreach (var (label, builtIn) in CommonCategories.Roster)
        {
            var check = OttawaWorkUi.CheckBoxItem(label);
            check.FontSize = 12;
            check.Margin = new Thickness(0, 2, 0, 2);
            _scopeCategoryChecks[builtIn] = check;
            categoryList.Children.Add(check);
        }
        categoryScroll.Content = categoryList;
        stack.Children.Add(OttawaWorkUi.Card(categoryScroll, padding: 8));

        stack.Children.Add(OttawaWorkUi.SectionHeader("Level", OttawaWorkUi.TextSecondary));
        _levelBox.Margin = new Thickness(0, 4, 0, 12);
        stack.Children.Add(_levelBox);

        var loadButton = OttawaWorkUi.PrimaryButton("Load elements");
        loadButton.Click += (_, _) => LoadElements();
        stack.Children.Add(loadButton);

        _scopeStatusText.Margin = new Thickness(0, 8, 0, 12);
        _scopeStatusText.TextWrapping = TextWrapping.Wrap;
        stack.Children.Add(_scopeStatusText);

        stack.Children.Add(OttawaWorkUi.SectionHeader("Loaded families / types"));
        var typeScroll = new ScrollViewer { MaxHeight = 320, Content = _familyTypePanel };
        stack.Children.Add(OttawaWorkUi.Card(typeScroll, padding: 8));

        return OttawaWorkUi.Card(stack, padding: 14);
    }

    private void SetAllCategories(bool isChecked)
    {
        foreach (var check in _scopeCategoryChecks.Values) check.IsChecked = isChecked;
    }

    private void RefreshLevelOptions()
    {
        _levelBox.Items.Clear();
        _levelIdsByIndex.Clear();

        _levelBox.Items.Add("(All Levels)");
        _levelIdsByIndex.Add(null);

        var levels = new FilteredElementCollector(_doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList();

        foreach (var level in levels)
        {
            _levelBox.Items.Add(level.Name);
            _levelIdsByIndex.Add(level.Id);
        }

        _levelBox.SelectedIndex = 0;
    }

    private void LoadElements()
    {
        var categories = _scopeCategoryChecks.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
        var levelId = _levelBox.SelectedIndex >= 0 && _levelBox.SelectedIndex < _levelIdsByIndex.Count
            ? _levelIdsByIndex[_levelBox.SelectedIndex]
            : null;

        _loadedElements = ParamPowerSuiteEngine.LoadElements(_doc, categories, levelId);

        RefreshFamilyTypeList();
        RefreshParameterOptions();

        _scopeStatusText.Text = categories.Count == 0
            ? "Pick at least one category, then Load elements."
            : $"{_loadedElements.Count} element(s) loaded across {categories.Count} categor{(categories.Count == 1 ? "y" : "ies")}.";

        SetStatus($"Loaded: {_loadedElements.Count}", 0, 0, 0);
    }

    private void RefreshFamilyTypeList()
    {
        _familyTypePanel.Children.Clear();
        var groups = ParamPowerSuiteEngine.GroupByFamilyType(_loadedElements);

        if (groups.Count == 0)
        {
            _familyTypePanel.Children.Add(OttawaWorkUi.FieldLabel("(no elements loaded)"));
            return;
        }

        foreach (var group in groups)
        {
            _familyTypePanel.Children.Add(new TextBlock
            {
                Text = $"{group.FamilyName} : {group.TypeName} ({group.Count})",
                FontSize = 11,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2),
            });
        }
    }

    /// <summary>Repopulates every tab's parameter-name dropdown from the union of parameter names
    /// on one sample element per distinct type currently loaded — cheap even with thousands of
    /// elements loaded, since real projects have far fewer distinct types than instances.</summary>
    private void RefreshParameterOptions()
    {
        var names = _loadedElements
            .GroupBy(e => e.GetTypeId())
            .Select(g => g.First())
            .SelectMany(e => e.Parameters.Cast<Parameter>())
            .Select(p => p.Definition.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        foreach (var box in _parameterDependentComboBoxes)
        {
            var previouslySelected = box.SelectedItem as string;
            box.Items.Clear();
            box.Items.AddRange(names.Cast<object>());
            box.SelectedIndex = previouslySelected is not null && names.Contains(previouslySelected)
                ? names.IndexOf(previouslySelected)
                : (names.Count > 0 ? 0 : -1);
        }
    }

    // ---------------- Shared bottom status bar ----------------

    private UIElement BuildStatusBar()
    {
        var border = new Border
        {
            Background = OttawaWorkUi.BrushOf(OttawaWorkUi.CardBackgroundAlt),
            BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.BorderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new System.Windows.CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 12, 0, 0),
        };

        var closeButton = OttawaWorkUi.SecondaryButton("Close");
        closeButton.HorizontalAlignment = HorizontalAlignment.Right;
        closeButton.Click += (_, _) => { DialogResult = true; Close(); };

        var statusHolder = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        statusHolder.Children.Add(_statusText);

        var barRow = new Grid();
        barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(statusHolder, 0);
        Grid.SetColumn(closeButton, 1);
        barRow.Children.Add(statusHolder);
        barRow.Children.Add(closeButton);

        border.Child = barRow;
        SetStatus("Ready.", 0, 0, 0);
        return border;
    }

    /// <summary>Called by every tab's Apply handler to refresh the shared status bar — the one
    /// place "Applied: X | Skipped: Y | Failed: Z" gets formatted, so every tab reads the same way.</summary>
    private void SetStatus(string prefix, int applied, int skipped, int failed)
        => _statusText.Text = $"{prefix} — Applied: {applied} | Skipped: {skipped} | Failed: {failed}";

    private void SetStatus(string prefix, ParamOpResult result) => SetStatus(prefix, result.Applied, result.Skipped, result.Failed);

    /// <summary>Shared by Find/Replace, Case Transform, Copy A→B, and Combine — the status bar's
    /// numeric "Failed: N" alone gives no way to tell a real bug from N values that just don't fit
    /// their target parameter, so this surfaces Revit's own reason (or the exception message) per
    /// failed element instead of leaving it a silent count, same as Color Code and Batch Excel Sync
    /// already do for their own failure paths.</summary>
    private static void ShowApplyFailures(string tabLabel, List<ParamPowerSuiteEngine.ApplyFailure> failures)
    {
        if (failures.Count == 0) return;

        const int maxShown = 15;
        var lines = failures.Take(maxShown).Select(f => $"{f.ElementLabel}: {f.Reason}");
        var text = string.Join("\n", lines);
        if (failures.Count > maxShown) text += $"\n…and {failures.Count - maxShown} more not shown.";

        System.Windows.MessageBox.Show(text, $"Ottawa Tools — Param Power Suite — {tabLabel}: {failures.Count} failure(s)");
    }

    /// <summary>Renders a preview list of pending old→new changes — shared by Find/Replace, Case
    /// Transform, Copy A→B, and Combine, since all four resolve to the same "old value → new value
    /// per element" shape before Apply writes them. Capped so a huge scope doesn't render thousands
    /// of rows into the WPF visual tree at once.</summary>
    private static UIElement BuildPreviewList(List<ParamChangePreview> changes)
    {
        const int maxRows = 200;

        var panel = new StackPanel();
        if (changes.Count == 0)
        {
            panel.Children.Add(OttawaWorkUi.FieldLabel("No changes — nothing would be modified with the current settings."));
            return panel;
        }

        foreach (var change in changes.Take(maxRows))
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock { Text = change.ElementLabel, FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary) });
            row.Children.Add(new TextBlock
            {
                Text = $"\"{change.OldValue}\"  →  \"{change.NewValue}\"",
                FontSize = 12,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary),
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(row);
        }

        if (changes.Count > maxRows)
            panel.Children.Add(OttawaWorkUi.FieldLabel($"…and {changes.Count - maxRows} more not shown (all {changes.Count} will still be applied)."));

        return panel;
    }
}
