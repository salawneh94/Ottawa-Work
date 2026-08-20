using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Slider = System.Windows.Controls.Slider;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Thickness = System.Windows.Thickness;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.OverrideByParam;

public enum ColorCodeAction { ApplyFilters, ExportPng, ClearFilters }

/// <summary>
/// Category + color-by-parameter picker with a live value legend (each
/// distinct value gets a swatch, an element count, and a checkbox so it
/// can be excluded), a transparency slider, and a choice of what to do:
/// apply as real persistent view filters (not one-off element overrides —
/// editable later in Visibility/Graphics like any other filter), export
/// the colored view as a PNG, or clear the filters this tool created.
/// </summary>
public class OverrideByParamWindow : BimFlowWindow
{
    private readonly Document _doc;
    private readonly View _view;
    private readonly Dictionary<string, Category> _categoriesByName;
    private readonly ComboBox _categoryBox = BimFlowUi.ComboBox();
    private readonly ComboBox _paramBox = BimFlowUi.ComboBox();
    private readonly Slider _transparencySlider = new() { Minimum = 0, Maximum = 100, Value = 0, TickFrequency = 5 };
    private readonly CheckBox _wipeFirstBox = BimFlowUi.CheckBoxItem("Remove this tool's existing filters on this category/parameter first", isChecked: true);
    private readonly StackPanel _legendPanel = new();
    private readonly Dictionary<string, (Autodesk.Revit.DB.Color Color, int Count, CheckBox Box)> _legendRows = new();

    public Category? SelectedCategory { get; private set; }
    public string? SelectedParameterName { get; private set; }
    public List<string> SelectedValues { get; private set; } = new();
    public Dictionary<string, Autodesk.Revit.DB.Color> ColorByValue { get; private set; } = new();
    public int Transparency { get; private set; }
    public bool WipeFirst { get; private set; }
    public ColorCodeAction ChosenAction { get; private set; }

    public OverrideByParamWindow(Document doc, View view) : base("BIMFlow — Color Code", minWidth: 620)
    {
        _doc = doc;
        _view = view;

        _categoriesByName = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .Select(e => e.Category)
            .Where(c => c is not null)
            .GroupBy(c => c!.Name)
            .Select(g => g.First()!)
            .OrderBy(c => c.Name)
            .ToDictionary(c => c.Name, c => c);

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🎨", "Color Code", "Color-code elements by parameter value using persistent view filters."));

        var columns = new StackPanel { Orientation = Orientation.Horizontal };

        var left = new StackPanel { Width = 280, Margin = new Thickness(0, 0, 16, 0) };
        left.Children.Add(BimFlowUi.FieldLabel("Category"));
        _categoryBox.Items.AddRange(_categoriesByName.Keys.Cast<object>().ToArray());
        _categoryBox.SelectionChanged += (_, _) => RefreshParameterOptions();
        left.Children.Add(_categoryBox);

        left.Children.Add(BimFlowUi.FieldLabel("Parameter"));
        _paramBox.SelectionChanged += (_, _) => RefreshLegend();
        left.Children.Add(_paramBox);

        left.Children.Add(BimFlowUi.FieldLabel("Transparency"));
        left.Children.Add(_transparencySlider);

        left.Children.Add(_wipeFirstBox);
        columns.Children.Add(left);

        var right = new StackPanel { Width = 280 };
        right.Children.Add(BimFlowUi.SectionHeader("Colour legend"));
        var legendScroll = new ScrollViewer { MaxHeight = 300, Content = _legendPanel };
        right.Children.Add(BimFlowUi.Card(legendScroll, padding: 8));

        var toggleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var allButton = BimFlowUi.SecondaryButton("All");
        var noneButton = BimFlowUi.SecondaryButton("None");
        allButton.Margin = new Thickness(0, 0, 8, 0);
        noneButton.Margin = new Thickness(0, 0, 8, 0);
        allButton.Click += (_, _) => { foreach (var row in _legendRows.Values) row.Box.IsChecked = true; };
        noneButton.Click += (_, _) => { foreach (var row in _legendRows.Values) row.Box.IsChecked = false; };
        toggleRow.Children.Add(allButton);
        toggleRow.Children.Add(noneButton);
        right.Children.Add(toggleRow);
        columns.Children.Add(right);

        root.Children.Add(columns);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var clearButton = BimFlowUi.SecondaryButton("Clear filters");
        var exportButton = BimFlowUi.SecondaryButton("Export PNG");
        var applyButton = BimFlowUi.PrimaryButton("Apply as filters");
        cancelButton.Margin = new Thickness(0, 0, 8, 0);
        clearButton.Margin = new Thickness(0, 0, 8, 0);
        exportButton.Margin = new Thickness(0, 0, 8, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        clearButton.Click += (_, _) => Finish(ColorCodeAction.ClearFilters);
        exportButton.Click += (_, _) => Finish(ColorCodeAction.ExportPng);
        applyButton.Click += (_, _) => Finish(ColorCodeAction.ApplyFilters);
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(clearButton);
        buttonRow.Children.Add(exportButton);
        buttonRow.Children.Add(applyButton);
        root.Children.Add(buttonRow);

        SetContent(root, padding: 24);

        if (_categoryBox.Items.Count > 0) _categoryBox.SelectedIndex = 0;
    }

    private void RefreshParameterOptions()
    {
        if (_categoryBox.SelectedItem is not string categoryName) return;
        var category = _categoriesByName[categoryName];

        var sample = new FilteredElementCollector(_doc, _view.Id)
            .WhereElementIsNotElementType()
            .OfCategoryId(category.Id)
            .FirstOrDefault();

        var names = sample is null
            ? new List<string>()
            : sample.Parameters.Cast<Parameter>().Select(p => p.Definition.Name).Distinct().OrderBy(n => n).ToList();

        _paramBox.Items.Clear();
        _paramBox.Items.AddRange(names.Cast<object>().ToArray());
        if (_paramBox.Items.Count > 0) _paramBox.SelectedIndex = 0;
        else RefreshLegend();
    }

    private void RefreshLegend()
    {
        _legendPanel.Children.Clear();
        _legendRows.Clear();

        if (_categoryBox.SelectedItem is not string categoryName || _paramBox.SelectedItem is not string paramName)
            return;
        var category = _categoriesByName[categoryName];

        var elements = new FilteredElementCollector(_doc, _view.Id)
            .WhereElementIsNotElementType()
            .OfCategoryId(category.Id)
            .ToList();

        var counts = elements
            .Select(e => e.LookupParameter(paramName)?.AsValueString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v!)
            .OrderBy(g => g.Key)
            .ToList();

        for (var i = 0; i < counts.Count; i++)
        {
            var value = counts[i].Key;
            var count = counts[i].Count();
            var color = ColorPalette.ForIndex(i);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            var swatch = new System.Windows.Controls.Border
            {
                Width = 14,
                Height = 14,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(color.Red, color.Green, color.Blue)),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var box = BimFlowUi.CheckBoxItem($"{value}  ({count})", isChecked: true);
            row.Children.Add(swatch);
            row.Children.Add(box);
            _legendPanel.Children.Add(row);
            _legendRows[value] = (color, count, box);
        }

        if (counts.Count == 0)
            _legendPanel.Children.Add(new TextBlock { Text = "No values found for this parameter in the active view.", Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), FontSize = 12, TextWrapping = System.Windows.TextWrapping.Wrap });
    }

    private void Finish(ColorCodeAction action)
    {
        SelectedCategory = _categoryBox.SelectedItem as string is { } name ? _categoriesByName[name] : null;
        SelectedParameterName = _paramBox.SelectedItem as string;
        Transparency = (int)_transparencySlider.Value;
        WipeFirst = _wipeFirstBox.IsChecked == true;
        ChosenAction = action;
        SelectedValues = _legendRows.Where(r => r.Value.Box.IsChecked == true).Select(r => r.Key).ToList();
        ColorByValue = _legendRows.ToDictionary(r => r.Key, r => r.Value.Color);
        DialogResult = true;
        Close();
    }
}
