using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.QuickSelect;

/// <summary>
/// Category + up to three AND-ed filter rules, plus a save/load/delete
/// preset panel — dark themed, replacing the old WinForms two-column
/// TableLayoutPanel dialog.
/// </summary>
public class QuickSelectWindow : BimFlowWindow
{
    private readonly Document _doc;
    private readonly Dictionary<string, Category> _categoriesByName;
    private readonly ComboBox _categoryBox = BimFlowUi.ComboBox();
    private readonly List<(ComboBox ParameterBox, ComboBox OperatorBox, TextBox ValueBox)> _rows = new();
    private readonly ListBox _presetList = new() { MaxHeight = 200 };
    private List<FilterPreset> _presets = PresetStore.Load();

    public Category? SelectedCategory { get; private set; }
    public List<FilterRule> Rules { get; private set; } = new();

    public QuickSelectWindow(Document doc) : base("BIMFlow — QuickSelect+", minWidth: 560)
    {
        _doc = doc;

        _categoriesByName = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Select(e => e.Category)
            .Where(c => c is not null)
            .GroupBy(c => c!.Name)
            .Select(g => g.First()!)
            .OrderBy(c => c.Name)
            .ToDictionary(c => c.Name, c => c);

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("⚡", "QuickSelect+", "Filter by category and up to three parameter rules, with saved presets."));

        var mainRow = new StackPanel { Orientation = Orientation.Horizontal };

        var rulesColumn = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        var categoryStack = new StackPanel();
        categoryStack.Children.Add(BimFlowUi.FieldLabel("Category"));
        _categoryBox.Items.AddRange(_categoriesByName.Keys.Cast<object>().ToArray());
        _categoryBox.SelectionChanged += CategoryBox_SelectionChanged;
        categoryStack.Children.Add(_categoryBox);
        rulesColumn.Children.Add(BimFlowUi.Card(categoryStack));

        var rulesStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        rulesStack.Children.Add(BimFlowUi.SectionHeader("Match ALL of these (AND)"));
        for (var i = 0; i < 3; i++)
        {
            var paramBox = BimFlowUi.ComboBox();
            paramBox.Width = 180;
            var opBox = BimFlowUi.ComboBox();
            opBox.Width = 90;
            opBox.Items.AddRange(Enum.GetNames<RuleOperator>().Cast<object>().ToArray());
            opBox.SelectedIndex = 0;
            var valueBox = BimFlowUi.TextBox();
            valueBox.Width = 120;

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            paramBox.Margin = new Thickness(0, 0, 6, 0);
            opBox.Margin = new Thickness(0, 0, 6, 0);
            row.Children.Add(paramBox);
            row.Children.Add(opBox);
            row.Children.Add(valueBox);
            rulesStack.Children.Add(row);

            _rows.Add((paramBox, opBox, valueBox));
        }
        rulesColumn.Children.Add(BimFlowUi.Card(rulesStack));
        mainRow.Children.Add(rulesColumn);

        var presetColumn = new StackPanel { Width = 180 };
        presetColumn.Children.Add(BimFlowUi.SectionHeader("Presets"));
        _presetList.Background = BimFlowUi.BrushOf(BimFlowUi.CardBackgroundAlt);
        _presetList.Foreground = BimFlowUi.BrushOf(BimFlowUi.TextPrimary);
        _presetList.BorderBrush = BimFlowUi.BrushOf(BimFlowUi.BorderColor);
        RefreshPresetList();
        presetColumn.Children.Add(BimFlowUi.Card(_presetList, padding: 4));

        var loadButton = BimFlowUi.SecondaryButton("Load");
        loadButton.Margin = new Thickness(0, 8, 0, 4);
        loadButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        loadButton.Click += (_, _) => LoadSelectedPreset();
        var saveButton = BimFlowUi.SecondaryButton("Save current as...");
        saveButton.Margin = new Thickness(0, 0, 0, 4);
        saveButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        saveButton.Click += (_, _) => SaveCurrentAsPreset();
        var deleteButton = BimFlowUi.SecondaryButton("Delete");
        deleteButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        deleteButton.Click += (_, _) => DeleteSelectedPreset();
        presetColumn.Children.Add(loadButton);
        presetColumn.Children.Add(saveButton);
        presetColumn.Children.Add(deleteButton);
        mainRow.Children.Add(presetColumn);

        root.Children.Add(mainRow);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var selectButton = BimFlowUi.PrimaryButton("Select in model");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        selectButton.Click += (_, _) => { Commit(); DialogResult = true; Close(); };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(selectButton);
        root.Children.Add(buttonRow);

        SetContent(root);

        if (_categoriesByName.Count > 0) _categoryBox.SelectedIndex = 0;
    }

    private void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshParameterOptions();

    private void RefreshParameterOptions()
    {
        if (_categoryBox.SelectedItem is not string categoryName) return;
        var category = _categoriesByName[categoryName];

        var sample = new FilteredElementCollector(_doc)
            .WhereElementIsNotElementType()
            .OfCategoryId(category.Id)
            .FirstOrDefault();

        var paramNames = sample is null
            ? new List<string>()
            : sample.Parameters.Cast<Parameter>().Select(p => p.Definition.Name).Distinct().OrderBy(n => n).ToList();

        foreach (var row in _rows)
        {
            var previous = row.ParameterBox.SelectedItem as string;
            row.ParameterBox.Items.Clear();
            row.ParameterBox.Items.Add("(unused)");
            row.ParameterBox.Items.AddRange(paramNames.Cast<object>().ToArray());
            row.ParameterBox.SelectedIndex = previous is not null && paramNames.Contains(previous)
                ? paramNames.IndexOf(previous) + 1
                : 0;
        }
    }

    private void Commit()
    {
        if (_categoryBox.SelectedItem is string categoryName)
            SelectedCategory = _categoriesByName[categoryName];

        Rules = _rows
            .Where(r => r.ParameterBox.SelectedIndex > 0)
            .Select(r => new FilterRule(
                (string)r.ParameterBox.SelectedItem!,
                Enum.Parse<RuleOperator>((string)r.OperatorBox.SelectedItem!),
                r.ValueBox.Text))
            .ToList();
    }

    private void RefreshPresetList()
    {
        _presetList.Items.Clear();
        foreach (var preset in _presets)
            _presetList.Items.Add(preset.Name);
    }

    private void LoadSelectedPreset()
    {
        if (_presetList.SelectedIndex < 0) return;
        var preset = _presets[_presetList.SelectedIndex];

        if (_categoriesByName.ContainsKey(preset.CategoryName))
            _categoryBox.SelectedItem = preset.CategoryName;
        RefreshParameterOptions();

        for (var i = 0; i < _rows.Count; i++)
        {
            if (i < preset.Rules.Count)
            {
                var rule = preset.Rules[i];
                var idx = _rows[i].ParameterBox.Items.IndexOf(rule.ParameterName);
                _rows[i].ParameterBox.SelectedIndex = idx >= 0 ? idx : 0;
                _rows[i].OperatorBox.SelectedItem = rule.Operator.ToString();
                _rows[i].ValueBox.Text = rule.Value;
            }
            else
            {
                _rows[i].ParameterBox.SelectedIndex = 0;
                _rows[i].ValueBox.Text = string.Empty;
            }
        }
    }

    private void SaveCurrentAsPreset()
    {
        Commit();
        if (SelectedCategory is null) return;

        var name = TextInputDialog.Prompt("Save preset", "Preset name:", "My preset");
        if (string.IsNullOrWhiteSpace(name)) return;

        _presets.RemoveAll(p => p.Name == name);
        _presets.Add(new FilterPreset(name, SelectedCategory.Name, Rules));
        PresetStore.Save(_presets);
        RefreshPresetList();
    }

    private void DeleteSelectedPreset()
    {
        if (_presetList.SelectedIndex < 0) return;
        _presets.RemoveAt(_presetList.SelectedIndex);
        PresetStore.Save(_presets);
        RefreshPresetList();
    }
}
