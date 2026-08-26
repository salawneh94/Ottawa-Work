using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using RadioButton = System.Windows.Controls.RadioButton;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Border = System.Windows.Controls.Border;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using TextWrapping = System.Windows.TextWrapping;
using UIElement = System.Windows.UIElement;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.UniqueNumbering;

public enum UniqueNumberingAction
{
    Assign,
    ClearValues,
}

/// <summary>
/// Rule-based batch numbering for any of the common model categories (not
/// just Rooms) — pick a category, a sort order, one or more numbering rules
/// (each its own target parameter + prefix/separator/start/step), preview
/// every element's existing vs. new value, then assign or clear in one pass.
/// Supersedes the old Room Renumber (Rooms only, one hardcoded field, no
/// preview) — Grid Renumber is untouched, since renaming grids/levels by
/// find/replace is a different operation from sequential numbering by sort
/// order.
/// </summary>
public class UniqueNumberingWindow : OttawaWorkWindow
{
    private static readonly (string Label, BuiltInCategory BuiltIn)[] CategoryRoster = CommonCategories.Roster;

    private readonly Document _doc;
    private readonly List<ElementId> _selectionIds;
    private readonly Dictionary<string, Category> _categoriesByName = new();

    private readonly ComboBox _categoryBox = OttawaWorkUi.ComboBox();
    private readonly TextBlock _categoryCountText = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, -6, 0, 10) };

    private readonly RadioButton _sortLevelLtr;
    private readonly RadioButton _sortLevelTtb;
    private readonly RadioButton _sortLevelTypeName;
    private readonly RadioButton _sortElementId;
    private readonly RadioButton _sortSelectionOrder;

    private readonly CheckBox _skipAlreadyNumberedBox = OttawaWorkUi.CheckBoxItem("Skip already-numbered", isChecked: true);
    private readonly CheckBox _scopeToSelectionBox;

    private readonly StackPanel _rulesPanel = new();
    private readonly List<RuleRow> _ruleRows = new();

    private readonly StackPanel _statsRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
    private readonly StackPanel _previewPanel = new();

    public List<NumberingRule> Rules { get; private set; } = new();
    public List<NumberingPreviewRow> PreviewRows { get; private set; } = new();
    public UniqueNumberingAction ChosenAction { get; private set; }

    private sealed record RuleRow(Border Card, ComboBox ParamBox, TextBox PrefixBox, TextBox SeparatorBox, TextBox StartBox, TextBox StepBox);

    public UniqueNumberingWindow(Document doc, ICollection<ElementId> selectionIds) : base("Ottawa Tools — Unique Numbering", minWidth: 760)
    {
        _doc = doc;
        _selectionIds = selectionIds.ToList();

        foreach (var (label, builtIn) in CategoryRoster)
        {
            var category = Category.GetCategory(doc, builtIn);
            if (category is not null) _categoriesByName[label] = category;
        }

        _scopeToSelectionBox = OttawaWorkUi.CheckBoxItem("Scope to selection only", isChecked: _selectionIds.Count > 0);

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🔢", "Unique Numbering", "Add numbering rules per parameter. Preview, then apply at once."));

        var columns = new StackPanel { Orientation = Orientation.Horizontal };

        var left = new StackPanel { Width = 340, Margin = new Thickness(0, 0, 16, 0) };

        left.Children.Add(OttawaWorkUi.SectionHeader("Category"));
        foreach (var (label, _) in CategoryRoster)
            if (_categoriesByName.ContainsKey(label)) _categoryBox.Items.Add(label);
        _categoryBox.SelectionChanged += (_, _) => { RefreshCategoryCount(); RefreshAllRuleParamOptions(); };
        left.Children.Add(_categoryBox);
        left.Children.Add(_categoryCountText);

        left.Children.Add(OttawaWorkUi.SectionHeader("Sort Order"));
        var sortStack = new StackPanel();
        _sortLevelLtr = OttawaWorkUi.RadioButtonItem("Level > L-R, T-B", "sort", isChecked: true);
        _sortLevelTtb = OttawaWorkUi.RadioButtonItem("Level > T-B, L-R", "sort");
        _sortLevelTypeName = OttawaWorkUi.RadioButtonItem("Level > Type Name", "sort");
        _sortElementId = OttawaWorkUi.RadioButtonItem("Element ID order", "sort");
        _sortSelectionOrder = OttawaWorkUi.RadioButtonItem("Selection order", "sort");
        sortStack.Children.Add(_sortLevelLtr);
        sortStack.Children.Add(_sortLevelTtb);
        sortStack.Children.Add(_sortLevelTypeName);
        sortStack.Children.Add(_sortElementId);
        sortStack.Children.Add(_sortSelectionOrder);
        left.Children.Add(OttawaWorkUi.Card(sortStack));

        left.Children.Add(OttawaWorkUi.SectionHeader("Options"));
        var optionsStack = new StackPanel();
        _scopeToSelectionBox.Click += (_, _) => RefreshCategoryCount();
        optionsStack.Children.Add(_skipAlreadyNumberedBox);
        optionsStack.Children.Add(_scopeToSelectionBox);
        left.Children.Add(OttawaWorkUi.Card(optionsStack, padding: 12));

        left.Children.Add(OttawaWorkUi.SectionHeader("Numbering Rules"));
        left.Children.Add(_rulesPanel);
        var addRuleButton = OttawaWorkUi.SecondaryButton("+ Add Rule");
        addRuleButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        addRuleButton.Margin = new Thickness(0, 0, 0, 12);
        addRuleButton.Click += (_, _) => AddRuleRow();
        left.Children.Add(addRuleButton);

        var previewButton = OttawaWorkUi.SecondaryButton("Generate Preview");
        previewButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        previewButton.Click += (_, _) => GeneratePreview();
        left.Children.Add(previewButton);

        columns.Children.Add(left);

        var right = new StackPanel { Width = 380 };

        var previewHeaderRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 6) };
        var clearLink = new TextBlock
        {
            Text = "Clear All Values",
            FontSize = 11,
            Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Danger),
            TextDecorations = System.Windows.TextDecorations.Underline,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        clearLink.MouseLeftButtonUp += (_, _) => ClearAllValues();
        previewHeaderRow.Children.Add(clearLink);

        right.Children.Add(OttawaWorkUi.SectionHeader("Preview Results"));
        right.Children.Add(previewHeaderRow);
        right.Children.Add(_statsRow);

        var previewScroll = new ScrollViewer { MaxHeight = 360, Content = _previewPanel };
        right.Children.Add(OttawaWorkUi.Card(previewScroll, padding: 10));

        columns.Children.Add(right);

        root.Children.Add(columns);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var assignButton = OttawaWorkUi.PrimaryButton("Assign All Numbers");
        cancelButton.Margin = new Thickness(0, 0, 8, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        assignButton.Click += (_, _) => AssignAllNumbers();
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(assignButton);
        root.Children.Add(buttonRow);

        SetContent(root, padding: 24);

        AddRuleRow();
        if (_categoryBox.Items.Count > 0) _categoryBox.SelectedIndex = 0;
        RefreshStatTiles(0, 0, 0, 0);
        SetPreviewMessage("Pick a category, set up your rule(s), then click \"Generate Preview\".");
    }

    private string? SelectedCategoryLabel() => _categoryBox.SelectedItem as string;

    private NumberingSortOrder SelectedSortOrder() => true switch
    {
        _ when _sortLevelTtb.IsChecked == true => NumberingSortOrder.LevelTopToBottomThenLeftToRight,
        _ when _sortLevelTypeName.IsChecked == true => NumberingSortOrder.LevelThenTypeName,
        _ when _sortElementId.IsChecked == true => NumberingSortOrder.ElementIdOrder,
        _ when _sortSelectionOrder.IsChecked == true => NumberingSortOrder.SelectionOrder,
        _ => NumberingSortOrder.LevelLeftToRightThenTopToBottom,
    };

    private List<Element> CollectElements(Category category)
    {
        if (_scopeToSelectionBox.IsChecked == true && _selectionIds.Count > 0)
        {
            return _selectionIds
                .Select(id => _doc.GetElement(id))
                .OfType<Element>()
                .Where(e => e.Category is not null && e.Category.Id == category.Id)
                .ToList();
        }

        return new FilteredElementCollector(_doc)
            .WhereElementIsNotElementType()
            .OfCategoryId(category.Id)
            .ToList();
    }

    private void RefreshCategoryCount()
    {
        if (SelectedCategoryLabel() is not { } name || !_categoriesByName.TryGetValue(name, out var category))
        {
            _categoryCountText.Text = "";
            return;
        }
        _categoryCountText.Text = $"{CollectElements(category).Count} element(s) found";
    }

    private List<string> GetRuleParameterOptions()
    {
        if (SelectedCategoryLabel() is not { } name || !_categoriesByName.TryGetValue(name, out var category)) return new();

        var sample = CollectElements(category).FirstOrDefault();
        if (sample is null) return new();

        return sample.Parameters.Cast<Parameter>()
            .Where(p => p is { StorageType: StorageType.String, IsReadOnly: false })
            .Select(p => p.Definition.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    private void RefreshAllRuleParamOptions()
    {
        var names = GetRuleParameterOptions();
        foreach (var row in _ruleRows)
        {
            var previouslySelected = row.ParamBox.SelectedItem as string;
            row.ParamBox.Items.Clear();
            row.ParamBox.Items.AddRange(names.Cast<object>());
            row.ParamBox.SelectedIndex = previouslySelected is not null && names.Contains(previouslySelected)
                ? names.IndexOf(previouslySelected)
                : (names.Count > 0 ? 0 : -1);
        }
    }

    private void AddRuleRow()
    {
        var paramBox = OttawaWorkUi.ComboBox();
        paramBox.Items.AddRange(GetRuleParameterOptions().Cast<object>());
        if (paramBox.Items.Count > 0) paramBox.SelectedIndex = 0;

        var prefixBox = OttawaWorkUi.TextBox();
        var separatorBox = OttawaWorkUi.TextBox();
        separatorBox.Text = "-";
        var startBox = OttawaWorkUi.TextBox();
        startBox.Text = "1";
        var stepBox = OttawaWorkUi.TextBox();
        stepBox.Text = "1";

        var content = new StackPanel();

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var removeButton = OttawaWorkUi.SecondaryButton("✕");
        headerRow.Children.Add(removeButton);
        content.Children.Add(headerRow);

        content.Children.Add(OttawaWorkUi.FieldLabel("Parameter"));
        content.Children.Add(paramBox);

        var fieldsRow = new StackPanel { Orientation = Orientation.Horizontal };
        var col1 = new StackPanel { Width = 76, Margin = new Thickness(0, 0, 6, 0) };
        col1.Children.Add(OttawaWorkUi.FieldLabel("Prefix"));
        col1.Children.Add(prefixBox);
        var col2 = new StackPanel { Width = 60, Margin = new Thickness(0, 0, 6, 0) };
        col2.Children.Add(OttawaWorkUi.FieldLabel("Separator"));
        col2.Children.Add(separatorBox);
        var col3 = new StackPanel { Width = 70, Margin = new Thickness(0, 0, 6, 0) };
        col3.Children.Add(OttawaWorkUi.FieldLabel("Start #"));
        col3.Children.Add(startBox);
        var col4 = new StackPanel { Width = 60 };
        col4.Children.Add(OttawaWorkUi.FieldLabel("Step"));
        col4.Children.Add(stepBox);
        fieldsRow.Children.Add(col1);
        fieldsRow.Children.Add(col2);
        fieldsRow.Children.Add(col3);
        fieldsRow.Children.Add(col4);
        content.Children.Add(fieldsRow);

        var card = OttawaWorkUi.Card(content, padding: 12);
        card.Margin = new Thickness(0, 0, 0, 10);

        var row = new RuleRow(card, paramBox, prefixBox, separatorBox, startBox, stepBox);
        removeButton.Click += (_, _) => RemoveRuleRow(row);

        _ruleRows.Add(row);
        _rulesPanel.Children.Add(card);
    }

    private void RemoveRuleRow(RuleRow row)
    {
        if (_ruleRows.Count <= 1) return;
        _ruleRows.Remove(row);
        _rulesPanel.Children.Remove(row.Card);
    }

    private List<NumberingRule> BuildRules()
    {
        var rules = new List<NumberingRule>();
        foreach (var row in _ruleRows)
        {
            if (row.ParamBox.SelectedItem is not string paramName) continue;
            var start = int.TryParse(row.StartBox.Text, out var s) ? s : 1;
            var step = int.TryParse(row.StepBox.Text, out var st) && st != 0 ? st : 1;
            rules.Add(new NumberingRule(paramName, row.PrefixBox.Text, row.SeparatorBox.Text, start, step));
        }
        return rules;
    }

    private void SetPreviewMessage(string text)
    {
        _previewPanel.Children.Clear();
        _previewPanel.Children.Add(new TextBlock { Text = text, FontSize = 12, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), TextWrapping = TextWrapping.Wrap });
    }

    private void RefreshStatTiles(int elements, int rules, int assigns, int skips)
    {
        _statsRow.Children.Clear();
        _statsRow.Children.Add(Spaced(OttawaWorkUi.StatTile(elements.ToString(), "ELEMENTS")));
        _statsRow.Children.Add(Spaced(OttawaWorkUi.StatTile(rules.ToString(), "RULES", OttawaWorkUi.Accent)));
        _statsRow.Children.Add(Spaced(OttawaWorkUi.StatTile(assigns.ToString(), "ASSIGNS", OttawaWorkUi.Success)));
        _statsRow.Children.Add(Spaced(OttawaWorkUi.StatTile(skips.ToString(), "SKIPS", OttawaWorkUi.Warning)));
    }

    private static UIElement Spaced(UIElement element)
    {
        if (element is Border border) border.Margin = new Thickness(0, 0, 8, 0);
        return element;
    }

    private void GeneratePreview()
    {
        if (SelectedCategoryLabel() is not { } name || !_categoriesByName.TryGetValue(name, out var category))
        {
            SetPreviewMessage("Pick a category first.");
            RefreshStatTiles(0, 0, 0, 0);
            Rules = new();
            PreviewRows = new();
            return;
        }

        var rules = BuildRules();
        if (rules.Count == 0)
        {
            SetPreviewMessage("Add at least one numbering rule (with a parameter picked) first.");
            RefreshStatTiles(0, 0, 0, 0);
            Rules = new();
            PreviewRows = new();
            return;
        }

        var elements = CollectElements(category);
        var ordered = UniqueNumberingEngine.OrderElements(_doc, elements, SelectedSortOrder(), _selectionIds);
        var skipAlreadyNumbered = _skipAlreadyNumberedBox.IsChecked == true;
        var (rows, assigns, skips) = UniqueNumberingEngine.BuildPreview(_doc, ordered, rules, skipAlreadyNumbered);

        Rules = rules;
        PreviewRows = rows;

        RefreshStatTiles(ordered.Count, rules.Count, assigns, skips);
        RenderPreviewRows(rows, rules);
    }

    private void RenderPreviewRows(List<NumberingPreviewRow> rows, List<NumberingRule> rules)
    {
        const int maxShown = 200;

        _previewPanel.Children.Clear();
        if (rows.Count == 0)
        {
            SetPreviewMessage("No elements matched the current category/scope.");
            return;
        }

        foreach (var row in rows.Take(maxShown))
        {
            var block = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            block.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(row.Level) ? row.Descriptor : $"{row.Descriptor}  ·  {row.Level}",
                FontSize = 10,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
                TextWrapping = TextWrapping.Wrap,
            });

            for (var i = 0; i < rules.Count; i++)
            {
                var existing = string.IsNullOrWhiteSpace(row.ExistingValues[i]) ? "(none)" : row.ExistingValues[i];
                var line = row.NewValues[i] is { } newValue
                    ? $"{rules[i].ParameterName}: \"{existing}\"  →  \"{newValue}\""
                    : $"{rules[i].ParameterName}: \"{existing}\"  (skip — already numbered)";

                block.Children.Add(new TextBlock
                {
                    Text = line,
                    FontSize = 12,
                    Foreground = OttawaWorkUi.BrushOf(row.NewValues[i] is null ? OttawaWorkUi.TextSecondary : OttawaWorkUi.TextPrimary),
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            _previewPanel.Children.Add(block);
        }

        if (rows.Count > maxShown)
            _previewPanel.Children.Add(new TextBlock
            {
                Text = $"…and {rows.Count - maxShown} more not shown (all {rows.Count} will still be assigned).",
                FontSize = 11,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
                TextWrapping = TextWrapping.Wrap,
            });
    }

    private void AssignAllNumbers()
    {
        GeneratePreview();
        if (Rules.Count == 0 || PreviewRows.Count == 0) return;

        ChosenAction = UniqueNumberingAction.Assign;
        DialogResult = true;
        Close();
    }

    private void ClearAllValues()
    {
        GeneratePreview();
        if (Rules.Count == 0 || PreviewRows.Count == 0) return;

        ChosenAction = UniqueNumberingAction.ClearValues;
        DialogResult = true;
        Close();
    }
}
