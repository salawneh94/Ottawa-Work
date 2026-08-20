using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Slider = System.Windows.Controls.Slider;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using Button = System.Windows.Controls.Button;
using Grid = System.Windows.Controls.Grid;
using ColumnDefinition = System.Windows.Controls.ColumnDefinition;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Thickness = System.Windows.Thickness;
using GridLength = System.Windows.GridLength;
using TextWrapping = System.Windows.TextWrapping;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.OverrideByParam;

public enum ColorCodeAction { Preview, ApplyFilters, ExportPng, ClearFilters }
public enum ColorCodeMode { ColorAndTransparency, ColorOnly }

/// <summary>
/// Category + color-by-parameter picker with a live value legend (each
/// distinct value gets a swatch, an element count, and a checkbox so it
/// can be excluded), a choice of color palette, a transparency slider, and
/// a choice of what to do: preview with one-off element overrides on the
/// active view (nothing persisted), apply as real persistent view filters
/// (editable later in Visibility/Graphics), export the colored view as a
/// PNG, or clear the filters this tool created.
/// </summary>
public class OverrideByParamWindow : BimFlowWindow
{
    public const string NoValueKey = "(No Value)";

    private readonly Document _doc;
    private readonly View _view;
    private readonly Dictionary<string, Category> _categoriesByName;
    private readonly ComboBox _modeBox = BimFlowUi.ComboBox();
    private readonly ComboBox _categoryBox = BimFlowUi.ComboBox();
    private readonly TextBlock _categoryCountText = new() { FontSize = 10, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Margin = new Thickness(0, -6, 0, 10) };
    private readonly ComboBox _paramBox = BimFlowUi.ComboBox();
    private readonly ComboBox _paletteBox = BimFlowUi.ComboBox();
    private readonly StackPanel _paletteSwatchRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
    private readonly Slider _transparencySlider = new() { Minimum = 0, Maximum = 100, Value = 0, TickFrequency = 5 };
    private readonly TextBlock _transparencyReadout = new() { FontSize = 11, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _wipeYesButton = BimFlowUi.SecondaryButton("Yes - Wipe");
    private readonly Button _wipeNoButton = BimFlowUi.SecondaryButton("No - Keep");
    private readonly StackPanel _legendPanel = new();
    private readonly TextBlock _legendSummaryBadgeText = new() { FontSize = 10, FontWeight = System.Windows.FontWeights.Medium, Foreground = BimFlowUi.BrushOf(BimFlowUi.Accent) };
    private readonly TextBlock _legendCaption = new() { FontSize = 10, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly Dictionary<string, (Autodesk.Revit.DB.Color Color, int Count, CheckBox Box)> _legendRows = new();

    private bool _wipeFirst = true;

    public Category? SelectedCategory { get; private set; }
    public string? SelectedParameterName { get; private set; }
    public List<string> SelectedValues { get; private set; } = new();
    public Dictionary<string, Autodesk.Revit.DB.Color> ColorByValue { get; private set; } = new();
    public int Transparency { get; private set; }
    public ColorCodeMode Mode { get; private set; }
    public bool WipeFirst { get; private set; }
    public ColorCodeAction ChosenAction { get; private set; }

    public OverrideByParamWindow(Document doc, View view) : base("BIMFlow — Color Code", minWidth: 660)
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

        var left = new StackPanel { Width = 300, Margin = new Thickness(0, 0, 16, 0) };

        left.Children.Add(BimFlowUi.SectionHeader("Mode"));
        _modeBox.Items.AddRange(new object[] { "Colour + Transparency", "Colour Only" });
        _modeBox.SelectedIndex = 0;
        left.Children.Add(_modeBox);

        left.Children.Add(BimFlowUi.SectionHeader("Category"));
        _categoryBox.Items.AddRange(_categoriesByName.Keys.Cast<object>().ToArray());
        _categoryBox.SelectionChanged += (_, _) => { RefreshCategoryCount(); RefreshParameterOptions(); };
        left.Children.Add(_categoryBox);
        left.Children.Add(_categoryCountText);

        left.Children.Add(BimFlowUi.SectionHeader("Parameter"));
        _paramBox.SelectionChanged += (_, _) => RefreshLegend();
        left.Children.Add(_paramBox);

        left.Children.Add(BimFlowUi.SectionHeader("Colour Palette"));
        _paletteBox.Items.AddRange(ColorPalette.Named.Keys.Cast<object>());
        _paletteBox.SelectedIndex = 0;
        _paletteBox.SelectionChanged += (_, _) => { RefreshPaletteSwatchRow(); RefreshLegend(); };
        left.Children.Add(_paletteBox);
        left.Children.Add(_paletteSwatchRow);

        left.Children.Add(BimFlowUi.SectionHeader("Transparency"));
        var transparencyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        _transparencySlider.Width = 220;
        _transparencySlider.ValueChanged += (_, _) => _transparencyReadout.Text = $"{(int)_transparencySlider.Value}%";
        _transparencyReadout.Text = "0%";
        transparencyRow.Children.Add(_transparencySlider);
        transparencyRow.Children.Add(_transparencyReadout);
        left.Children.Add(transparencyRow);

        left.Children.Add(BimFlowUi.SectionHeader("Element Overrides"));
        left.Children.Add(new TextBlock { Text = "Revit overrides beat filters. Wipe first?", FontSize = 11, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap });
        var wipeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        _wipeYesButton.Margin = new Thickness(0, 0, 8, 0);
        _wipeYesButton.Click += (_, _) => SetWipeFirst(true);
        _wipeNoButton.Click += (_, _) => SetWipeFirst(false);
        wipeRow.Children.Add(_wipeYesButton);
        wipeRow.Children.Add(_wipeNoButton);
        left.Children.Add(wipeRow);
        SetWipeFirst(true);

        var previewButton = BimFlowUi.SecondaryButton("Preview");
        previewButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        previewButton.Click += (_, _) => Finish(ColorCodeAction.Preview);
        left.Children.Add(previewButton);

        columns.Children.Add(left);

        var right = new StackPanel { Width = 320 };

        var legendHeaderRow = new Grid();
        legendHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, System.Windows.GridUnitType.Star) });
        legendHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var legendHeader = BimFlowUi.SectionHeader("Colour Legend");
        Grid.SetColumn((System.Windows.UIElement)legendHeader, 0);
        var summaryBadge = BimFlowUi.Card(_legendSummaryBadgeText, padding: 4);
        summaryBadge.CornerRadius = new System.Windows.CornerRadius(10);
        Grid.SetColumn(summaryBadge, 1);
        legendHeaderRow.Children.Add((System.Windows.UIElement)legendHeader);
        legendHeaderRow.Children.Add(summaryBadge);
        right.Children.Add(legendHeaderRow);

        var linkRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 6) };
        linkRow.Children.Add(LinkText("All", () => { foreach (var row in _legendRows.Values) row.Box.IsChecked = true; RefreshLegendSummary(); }));
        linkRow.Children.Add(new TextBlock { Text = " · ", FontSize = 11, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary) });
        linkRow.Children.Add(LinkText("None", () => { foreach (var row in _legendRows.Values) row.Box.IsChecked = false; RefreshLegendSummary(); }));
        linkRow.Children.Add(new TextBlock { Text = " · ", FontSize = 11, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary) });
        linkRow.Children.Add(LinkText("Invert", () => { foreach (var row in _legendRows.Values) row.Box.IsChecked = row.Box.IsChecked != true; RefreshLegendSummary(); }));
        right.Children.Add(linkRow);

        var legendScroll = new ScrollViewer { MaxHeight = 300, Content = _legendPanel };
        right.Children.Add(BimFlowUi.Card(legendScroll, padding: 8));
        right.Children.Add(_legendCaption);

        columns.Children.Add(right);

        root.Children.Add(columns);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var clearButton = BimFlowUi.DangerButton("Clear filters");
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

        RefreshPaletteSwatchRow();
        if (_categoryBox.Items.Count > 0) _categoryBox.SelectedIndex = 0;
        RefreshCategoryCount();
    }

    private static TextBlock LinkText(string text, Action onClick)
    {
        var link = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = BimFlowUi.BrushOf(BimFlowUi.Accent),
            TextDecorations = System.Windows.TextDecorations.Underline,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        link.MouseLeftButtonUp += (_, _) => onClick();
        return link;
    }

    private void SetWipeFirst(bool wipe)
    {
        _wipeFirst = wipe;
        BimFlowUi.SetToggleActive(_wipeYesButton, wipe);
        BimFlowUi.SetToggleActive(_wipeNoButton, !wipe);
    }

    private void RefreshPaletteSwatchRow()
    {
        _paletteSwatchRow.Children.Clear();
        if (_paletteBox.SelectedItem is not string paletteName) return;
        var colors = ColorPalette.Named[paletteName];
        foreach (var color in colors)
        {
            _paletteSwatchRow.Children.Add(new System.Windows.Controls.Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new System.Windows.CornerRadius(3),
                Margin = new Thickness(0, 0, 4, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(color.Red, color.Green, color.Blue)),
            });
        }
    }

    private void RefreshCategoryCount()
    {
        if (_categoryBox.SelectedItem is not string categoryName)
        {
            _categoryCountText.Text = "";
            return;
        }
        var category = _categoriesByName[categoryName];
        var count = new FilteredElementCollector(_doc, _view.Id).WhereElementIsNotElementType().OfCategoryId(category.Id).GetElementCount();
        _categoryCountText.Text = $"({count} in view)";
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

        if (_categoryBox.SelectedItem is not string categoryName || _paramBox.SelectedItem is not string paramName || _paletteBox.SelectedItem is not string paletteName)
        {
            RefreshLegendSummary();
            return;
        }
        var category = _categoriesByName[categoryName];

        var elements = new FilteredElementCollector(_doc, _view.Id)
            .WhereElementIsNotElementType()
            .OfCategoryId(category.Id)
            .ToList();

        var byValue = elements
            .Select(e => e.LookupParameter(paramName)?.AsValueString())
            .Select(v => string.IsNullOrWhiteSpace(v) ? NoValueKey : v!)
            .GroupBy(v => v)
            .OrderBy(g => g.Key == NoValueKey ? 1 : 0)
            .ThenBy(g => g.Key)
            .ToList();

        for (var i = 0; i < byValue.Count; i++)
        {
            var value = byValue[i].Key;
            var count = byValue[i].Count();
            var isNoValue = value == NoValueKey;
            var color = isNoValue ? new Autodesk.Revit.DB.Color(150, 150, 150) : ColorPalette.ForIndex(i, paletteName);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            var swatch = new System.Windows.Controls.Border
            {
                Width = 14,
                Height = 14,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(color.Red, color.Green, color.Blue)),
            };
            var box = BimFlowUi.CheckBoxItem(value, isChecked: true);
            box.FontStyle = isNoValue ? System.Windows.FontStyles.Italic : System.Windows.FontStyles.Normal;
            box.Click += (_, _) => RefreshLegendSummary();
            var countText = new TextBlock { Text = count.ToString(), FontSize = 11, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), HorizontalAlignment = HorizontalAlignment.Right, Width = 40 };
            row.Children.Add(swatch);
            row.Children.Add(box);
            row.Children.Add(countText);
            _legendPanel.Children.Add(row);
            _legendRows[value] = (color, count, box);
        }

        if (byValue.Count == 0)
            _legendPanel.Children.Add(new TextBlock { Text = "No elements of this category are in the active view.", Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), FontSize = 12, TextWrapping = TextWrapping.Wrap });

        RefreshLegendSummary();
    }

    private void RefreshLegendSummary()
    {
        var valueCount = _legendRows.Keys.Count(k => k != NoValueKey);
        var elementCount = _legendRows.Values.Sum(r => r.Count);
        _legendSummaryBadgeText.Text = $"{valueCount} values · {elementCount} elements";

        var noValueCount = _legendRows.TryGetValue(NoValueKey, out var noValueRow) ? noValueRow.Count : 0;
        _legendCaption.Text = noValueCount > 0
            ? $"{noValueCount} element(s) have no value for this parameter — review before applying."
            : "All in-view elements resolved a value for this parameter.";
    }

    private void Finish(ColorCodeAction action)
    {
        SelectedCategory = _categoryBox.SelectedItem as string is { } name ? _categoriesByName[name] : null;
        SelectedParameterName = _paramBox.SelectedItem as string;
        Transparency = (int)_transparencySlider.Value;
        Mode = _modeBox.SelectedIndex == 1 ? ColorCodeMode.ColorOnly : ColorCodeMode.ColorAndTransparency;
        WipeFirst = _wipeFirst;
        ChosenAction = action;
        SelectedValues = _legendRows.Where(r => r.Value.Box.IsChecked == true).Select(r => r.Key).ToList();
        ColorByValue = _legendRows.ToDictionary(r => r.Key, r => r.Value.Color);
        DialogResult = true;
        Close();
    }
}
