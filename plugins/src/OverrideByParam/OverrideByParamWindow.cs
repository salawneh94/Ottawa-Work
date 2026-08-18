using ComboBox = System.Windows.Controls.ComboBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.OverrideByParam;

/// <summary>Category + color-by parameter picker — dark themed, replacing the old WinForms TableLayoutPanel dialog.</summary>
public class OverrideByParamWindow : BimFlowWindow
{
    private readonly Document _doc;
    private readonly Dictionary<string, Category> _categoriesByName;
    private readonly ComboBox _categoryBox = BimFlowUi.ComboBox();
    private readonly ComboBox _paramBox = BimFlowUi.ComboBox();

    public Category? SelectedCategory { get; private set; }
    public string? SelectedParameterName { get; private set; }

    public OverrideByParamWindow(Document doc) : base("BIMFlow — OverrideByParam", minWidth: 380)
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
        root.Children.Add(BimFlowUi.TitleBar("🎨", "Override by Parameter", "Color every element of a category in the active view by a parameter's value."));

        var fieldsStack = new StackPanel();
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Category"));
        _categoryBox.Items.AddRange(_categoriesByName.Keys.Cast<object>().ToArray());
        _categoryBox.SelectionChanged += CategoryBox_SelectionChanged;
        fieldsStack.Children.Add(_categoryBox);
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Color by parameter"));
        fieldsStack.Children.Add(_paramBox);
        root.Children.Add(BimFlowUi.Card(fieldsStack));

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

        var names = sample is null
            ? new List<string>()
            : sample.Parameters.Cast<Parameter>().Select(p => p.Definition.Name).Distinct().OrderBy(n => n).ToList();

        _paramBox.Items.Clear();
        _paramBox.Items.AddRange(names.Cast<object>().ToArray());
        if (_paramBox.Items.Count > 0) _paramBox.SelectedIndex = 0;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedCategory = _categoryBox.SelectedItem as string is { } name ? _categoriesByName[name] : null;
        SelectedParameterName = _paramBox.SelectedItem as string;
        DialogResult = true;
        Close();
    }
}
