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

namespace BIMFlow.ParameterMapper;

/// <summary>
/// Parameter-to-copy + target category + up to two filter rules — dark
/// themed, replacing the old WinForms TableLayoutPanel grid dialog.
/// </summary>
public class ParameterMapperWindow : BimFlowWindow
{
    private readonly Document _doc;
    private readonly Element _source;
    private readonly Dictionary<string, Category> _categoriesByName;
    private readonly ComboBox _paramBox = BimFlowUi.ComboBox();
    private readonly ComboBox _targetCategoryBox = BimFlowUi.ComboBox();
    private readonly System.Windows.Controls.TextBlock _sourceValueLabel;
    private readonly List<(ComboBox ParameterBox, ComboBox OperatorBox, TextBox ValueBox)> _rows = new();

    public string? ParameterName { get; private set; }
    public Category? TargetCategory { get; private set; }
    public List<FilterRule> FilterRules { get; private set; } = new();

    public ParameterMapperWindow(Document doc, Element source) : base("BIMFlow — ParameterMapper", minWidth: 460)
    {
        _doc = doc;
        _source = source;

        _categoriesByName = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Select(e => e.Category)
            .Where(c => c is not null)
            .GroupBy(c => c!.Name)
            .Select(g => g.First()!)
            .OrderBy(c => c.Name)
            .ToDictionary(c => c.Name, c => c);

        var sharedParamNames = source.Parameters.Cast<Parameter>()
            .Where(p => !p.IsReadOnly)
            .Select(p => p.Definition.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🔗", "Parameter Mapper", $"Source: {source.Name} ({source.Category?.Name})"));

        var sourceStack = new StackPanel();
        sourceStack.Children.Add(BimFlowUi.FieldLabel("Parameter to copy"));
        _paramBox.Items.AddRange(sharedParamNames.Cast<object>().ToArray());
        _paramBox.SelectionChanged += (_, _) => UpdateSourceValueLabel();
        sourceStack.Children.Add(_paramBox);
        if (_paramBox.Items.Count > 0) _paramBox.SelectedIndex = 0;

        _sourceValueLabel = BimFlowUi.FieldLabel("");
        sourceStack.Children.Add(_sourceValueLabel);
        root.Children.Add(BimFlowUi.Card(sourceStack));

        var targetStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        targetStack.Children.Add(BimFlowUi.FieldLabel("Copy to category"));
        _targetCategoryBox.Items.AddRange(_categoriesByName.Keys.Cast<object>().ToArray());
        _targetCategoryBox.SelectionChanged += (_, _) => RefreshRuleOptions();
        targetStack.Children.Add(_targetCategoryBox);
        if (_targetCategoryBox.Items.Count > 0) _targetCategoryBox.SelectedIndex = 0;
        root.Children.Add(BimFlowUi.Card(targetStack));

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
        var copyButton = BimFlowUi.PrimaryButton("Copy value");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        copyButton.Click += CopyButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(copyButton);
        root.Children.Add(buttonRow);

        SetContent(root);

        UpdateSourceValueLabel();
        RefreshRuleOptions();
    }

    private void UpdateSourceValueLabel()
    {
        if (_paramBox.SelectedItem is not string paramName) return;
        var value = _source.LookupParameter(paramName)?.AsValueString() ?? "(empty)";
        _sourceValueLabel.Text = $"Current value: {value}";
    }

    private void RefreshRuleOptions()
    {
        if (_targetCategoryBox.SelectedItem is not string categoryName) return;
        var category = _categoriesByName[categoryName];

        var sample = new FilteredElementCollector(_doc)
            .WhereElementIsNotElementType()
            .OfCategoryId(category.Id)
            .FirstOrDefault();

        var names = sample is null
            ? new List<string>()
            : sample.Parameters.Cast<Parameter>().Select(p => p.Definition.Name).Distinct().OrderBy(n => n).ToList();

        foreach (var row in _rows)
        {
            row.ParameterBox.Items.Clear();
            row.ParameterBox.Items.Add("(unused)");
            row.ParameterBox.Items.AddRange(names.Cast<object>().ToArray());
            row.ParameterBox.SelectedIndex = 0;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        ParameterName = _paramBox.SelectedItem as string;

        if (_targetCategoryBox.SelectedItem is string categoryName)
            TargetCategory = _categoriesByName[categoryName];

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
