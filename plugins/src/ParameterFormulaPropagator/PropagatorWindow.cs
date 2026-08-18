using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.ParameterFormulaPropagator;

/// <summary>
/// Category + template string + target parameter + start sequence + up to
/// two filter rules — dark themed, replacing the old WinForms
/// TableLayoutPanel grid dialog. WPF has no built-in NumericUpDown, so the
/// start-sequence field is a plain text box parsed as an int (defaulting
/// to 1 on anything that doesn't parse).
/// </summary>
public class PropagatorWindow : BimFlowWindow
{
    private readonly Document _doc;
    private readonly Dictionary<string, Category> _categoriesByName;
    private readonly ComboBox _categoryBox = BimFlowUi.ComboBox();
    private readonly ComboBox _targetParamBox = BimFlowUi.ComboBox();
    private readonly TextBox _templateBox = BimFlowUi.TextBox();
    private readonly TextBox _startBox = BimFlowUi.TextBox();
    private readonly List<(ComboBox ParameterBox, ComboBox OperatorBox, TextBox ValueBox)> _rows = new();

    public Category? SelectedCategory { get; private set; }
    public string? TargetParameterName { get; private set; }
    public string Template { get; private set; } = "";
    public int StartNumber { get; private set; } = 1;
    public List<FilterRule> FilterRules { get; private set; } = new();

    public PropagatorWindow(Document doc) : base("BIMFlow — ParameterFormulaPropagator", minWidth: 460)
    {
        _doc = doc;
        _templateBox.Text = "{Level}-EQ-{seq:000}";
        _startBox.Text = "1";

        _categoriesByName = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Select(e => e.Category)
            .Where(c => c is not null)
            .GroupBy(c => c!.Name)
            .Select(g => g.First()!)
            .OrderBy(c => c.Name)
            .ToDictionary(c => c.Name, c => c);

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🔤", "Parameter Formula Propagator", "Fill a text parameter from a template with {Level}, {Category}, and {seq} placeholders."));

        var categoryStack = new StackPanel();
        categoryStack.Children.Add(BimFlowUi.FieldLabel("Category"));
        _categoryBox.Items.AddRange(_categoriesByName.Keys.Cast<object>().ToArray());
        _categoryBox.SelectionChanged += CategoryBox_SelectionChanged;
        categoryStack.Children.Add(_categoryBox);
        root.Children.Add(BimFlowUi.Card(categoryStack));

        var templateStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        templateStack.Children.Add(BimFlowUi.SectionHeader("Template"));
        templateStack.Children.Add(_templateBox);
        templateStack.Children.Add(BimFlowUi.FieldLabel("Placeholders: {Level} {Category} {seq} {seq:000}"));
        templateStack.Children.Add(BimFlowUi.FieldLabel("Set parameter"));
        templateStack.Children.Add(_targetParamBox);
        templateStack.Children.Add(BimFlowUi.FieldLabel("Start sequence at"));
        templateStack.Children.Add(_startBox);
        root.Children.Add(BimFlowUi.Card(templateStack));

        var filterStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        filterStack.Children.Add(BimFlowUi.SectionHeader("Filter (optional, matches ALL rows)"));
        for (var i = 0; i < 2; i++)
        {
            var paramBox = BimFlowUi.ComboBox();
            paramBox.Width = 160;
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
            filterStack.Children.Add(row);

            _rows.Add((paramBox, opBox, valueBox));
        }
        root.Children.Add(BimFlowUi.Card(filterStack));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var applyButton = BimFlowUi.PrimaryButton("Apply");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        applyButton.Click += ApplyButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(applyButton);
        root.Children.Add(buttonRow);

        SetContent(root);

        if (_categoryBox.Items.Count > 0) _categoryBox.SelectedIndex = 0;
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

        var allNames = sample is null
            ? new List<string>()
            : sample.Parameters.Cast<Parameter>().Select(p => p.Definition.Name).Distinct().OrderBy(n => n).ToList();
        var editableNames = sample is null
            ? new List<string>()
            : sample.Parameters.Cast<Parameter>().Where(p => !p.IsReadOnly && p.StorageType == StorageType.String)
                .Select(p => p.Definition.Name).Distinct().OrderBy(n => n).ToList();

        foreach (var row in _rows)
        {
            row.ParameterBox.Items.Clear();
            row.ParameterBox.Items.Add("(unused)");
            row.ParameterBox.Items.AddRange(allNames.Cast<object>().ToArray());
            row.ParameterBox.SelectedIndex = 0;
        }

        _targetParamBox.Items.Clear();
        _targetParamBox.Items.AddRange(editableNames.Cast<object>().ToArray());
        if (_targetParamBox.Items.Count > 0) _targetParamBox.SelectedIndex = 0;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_categoryBox.SelectedItem is string categoryName)
            SelectedCategory = _categoriesByName[categoryName];

        TargetParameterName = _targetParamBox.SelectedItem as string;
        Template = _templateBox.Text;
        StartNumber = int.TryParse(_startBox.Text, out var n) && n >= 1 ? n : 1;

        FilterRules = _rows
            .Where(r => r.ParameterBox.SelectedIndex > 0)
            .Select(r => new FilterRule(
                (string)r.ParameterBox.SelectedItem!,
                Enum.Parse<RuleOperator>((string)r.OperatorBox.SelectedItem!),
                r.ValueBox.Text))
            .ToList();

        DialogResult = true;
        Close();
    }
}
