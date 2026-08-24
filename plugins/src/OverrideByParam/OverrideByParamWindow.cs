using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
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
using OttawaWork.Shared;

namespace OttawaWork.OverrideByParam;

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
public class OverrideByParamWindow : OttawaWorkWindow
{
    public const string NoValueKey = "(No Value)";

    /// <summary>
    /// The shared common-categories roster (OttawaWork.Shared.CommonCategories),
    /// offered in the same fixed order every time regardless of what's
    /// actually in the model — matching the reference tool's Category
    /// dropdown. Categories with zero elements in the active view are still
    /// listed (so the roster doesn't change shape project-to-project) but
    /// shown muted/disabled with their live count, rather than silently
    /// omitted the way an earlier view-scoped version of this dropdown did.
    /// </summary>
    private static readonly (string Label, BuiltInCategory BuiltIn)[] CategoryRoster = CommonCategories.Roster;

    private readonly Document _doc;
    private readonly View _view;
    private readonly Dictionary<string, Category> _categoriesByName;
    private readonly ComboBox _modeBox = OttawaWorkUi.ComboBox();
    private readonly ComboBox _categoryBox = OttawaWorkUi.ComboBox();
    private readonly TextBlock _categoryCountText = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, -6, 0, 10) };
    private readonly ComboBox _paramBox = OttawaWorkUi.ComboBox();
    private readonly ComboBox _paletteBox = OttawaWorkUi.ComboBox();
    private readonly StackPanel _paletteSwatchRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
    private readonly Slider _transparencySlider = new() { Minimum = 0, Maximum = 100, Value = 0, TickFrequency = 5 };
    private readonly TextBlock _transparencyReadout = new() { FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _wipeYesButton = OttawaWorkUi.SecondaryButton("Yes - Wipe");
    private readonly Button _wipeNoButton = OttawaWorkUi.SecondaryButton("No - Keep");
    private readonly StackPanel _legendPanel = new();
    private readonly TextBlock _legendSummaryBadgeText = new() { FontSize = 10, FontWeight = System.Windows.FontWeights.Medium, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent) };
    private readonly TextBlock _legendCaption = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
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

    public OverrideByParamWindow(Document doc, View view) : base("Ottawa Tools — Color Code", minWidth: 660)
    {
        _doc = doc;
        _view = view;

        _categoriesByName = new Dictionary<string, Category>();
        foreach (var (label, builtIn) in CategoryRoster)
        {
            var category = Category.GetCategory(doc, builtIn);
            if (category is not null) _categoriesByName[label] = category;
        }

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🎨", "Color Code", "Color-code elements by parameter value using persistent view filters."));

        var columns = new StackPanel { Orientation = Orientation.Horizontal };

        var left = new StackPanel { Width = 300, Margin = new Thickness(0, 0, 16, 0) };

        left.Children.Add(OttawaWorkUi.SectionHeader("Mode"));
        _modeBox.Items.AddRange(new object[] { "Colour + Transparency", "Colour Only" });
        _modeBox.SelectedIndex = 0;
        left.Children.Add(_modeBox);

        left.Children.Add(OttawaWorkUi.SectionHeader("Category"));
        var firstNonEmptyIndex = 0;
        for (var i = 0; i < CategoryRoster.Length; i++)
        {
            var (label, _) = CategoryRoster[i];
            if (!_categoriesByName.TryGetValue(label, out var category)) continue;

            var count = new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType().OfCategoryId(category.Id).GetElementCount();
            var isEmpty = count == 0;
            var item = new ComboBoxItem
            {
                Content = isEmpty ? $"{label} — 0 in view" : label,
                Tag = label,
                IsEnabled = !isEmpty,
                Foreground = OttawaWorkUi.BrushOf(isEmpty ? OttawaWorkUi.TextSecondary : OttawaWorkUi.TextPrimary),
                FontStyle = isEmpty ? System.Windows.FontStyles.Italic : System.Windows.FontStyles.Normal,
            };
            _categoryBox.Items.Add(item);
            if (!isEmpty && firstNonEmptyIndex == 0) firstNonEmptyIndex = _categoryBox.Items.Count - 1;
        }
        _categoryBox.SelectionChanged += (_, _) => { RefreshCategoryCount(); RefreshParameterOptions(); };
        left.Children.Add(_categoryBox);
        left.Children.Add(_categoryCountText);

        left.Children.Add(OttawaWorkUi.SectionHeader("Parameter"));
        _paramBox.SelectionChanged += (_, _) => RefreshLegend();
        left.Children.Add(_paramBox);

        left.Children.Add(OttawaWorkUi.SectionHeader("Colour Palette"));
        _paletteBox.Items.AddRange(ColorPalette.Named.Keys.Cast<object>());
        _paletteBox.SelectedIndex = 0;
        _paletteBox.SelectionChanged += (_, _) => { RefreshPaletteSwatchRow(); RefreshLegend(); };
        left.Children.Add(_paletteBox);
        left.Children.Add(_paletteSwatchRow);

        left.Children.Add(OttawaWorkUi.SectionHeader("Transparency"));
        var transparencyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        _transparencySlider.Width = 220;
        _transparencySlider.ValueChanged += (_, _) => _transparencyReadout.Text = $"{(int)_transparencySlider.Value}%";
        _transparencyReadout.Text = "0%";
        transparencyRow.Children.Add(_transparencySlider);
        transparencyRow.Children.Add(_transparencyReadout);
        left.Children.Add(transparencyRow);

        left.Children.Add(OttawaWorkUi.SectionHeader("Element Overrides"));
        left.Children.Add(new TextBlock { Text = "Revit overrides beat filters. Wipe first?", FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap });
        var wipeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        _wipeYesButton.Margin = new Thickness(0, 0, 8, 0);
        _wipeYesButton.Click += (_, _) => SetWipeFirst(true);
        _wipeNoButton.Click += (_, _) => SetWipeFirst(false);
        wipeRow.Children.Add(_wipeYesButton);
        wipeRow.Children.Add(_wipeNoButton);
        left.Children.Add(wipeRow);
        SetWipeFirst(true);

        var previewButton = OttawaWorkUi.SecondaryButton("Preview");
        previewButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        previewButton.Click += (_, _) => Finish(ColorCodeAction.Preview);
        left.Children.Add(previewButton);

        columns.Children.Add(left);

        var right = new StackPanel { Width = 320 };

        var legendHeaderRow = new Grid();
        legendHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, System.Windows.GridUnitType.Star) });
        legendHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var legendHeader = OttawaWorkUi.SectionHeader("Colour Legend");
        Grid.SetColumn((System.Windows.UIElement)legendHeader, 0);
        var summaryBadge = OttawaWorkUi.Card(_legendSummaryBadgeText, padding: 4);
        summaryBadge.CornerRadius = new System.Windows.CornerRadius(10);
        Grid.SetColumn(summaryBadge, 1);
        legendHeaderRow.Children.Add((System.Windows.UIElement)legendHeader);
        legendHeaderRow.Children.Add(summaryBadge);
        right.Children.Add(legendHeaderRow);

        var linkRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 6) };
        linkRow.Children.Add(LinkText("All", () => { foreach (var row in _legendRows.Values) row.Box.IsChecked = true; RefreshLegendSummary(); }));
        linkRow.Children.Add(new TextBlock { Text = " · ", FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary) });
        linkRow.Children.Add(LinkText("None", () => { foreach (var row in _legendRows.Values) row.Box.IsChecked = false; RefreshLegendSummary(); }));
        linkRow.Children.Add(new TextBlock { Text = " · ", FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary) });
        linkRow.Children.Add(LinkText("Invert", () => { foreach (var row in _legendRows.Values) row.Box.IsChecked = row.Box.IsChecked != true; RefreshLegendSummary(); }));
        right.Children.Add(linkRow);

        var legendScroll = new ScrollViewer { MaxHeight = 300, Content = _legendPanel };
        right.Children.Add(OttawaWorkUi.Card(legendScroll, padding: 8));
        right.Children.Add(_legendCaption);

        columns.Children.Add(right);

        root.Children.Add(columns);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var clearButton = OttawaWorkUi.DangerButton("Clear filters");
        var exportButton = OttawaWorkUi.SecondaryButton("Export PNG");
        var applyButton = OttawaWorkUi.PrimaryButton("Apply as filters");
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
        if (_categoryBox.Items.Count > 0) _categoryBox.SelectedIndex = firstNonEmptyIndex;
        RefreshCategoryCount();
    }

    private static TextBlock LinkText(string text, Action onClick)
    {
        var link = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent),
            TextDecorations = System.Windows.TextDecorations.Underline,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        link.MouseLeftButtonUp += (_, _) => onClick();
        return link;
    }

    private void SetWipeFirst(bool wipe)
    {
        _wipeFirst = wipe;
        OttawaWorkUi.SetToggleActive(_wipeYesButton, wipe);
        OttawaWorkUi.SetToggleActive(_wipeNoButton, !wipe);
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

    /// <summary>The Category combo now holds styled ComboBoxItems (for the grey-out-when-empty look) instead of
    /// plain strings, so its selected label comes off the item's Tag rather than a direct string cast.</summary>
    private string? SelectedCategoryLabel() => (_categoryBox.SelectedItem as ComboBoxItem)?.Tag as string;

    private void RefreshCategoryCount()
    {
        if (SelectedCategoryLabel() is not { } categoryName)
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
        if (SelectedCategoryLabel() is not { } categoryName) return;
        var category = _categoriesByName[categoryName];

        var sample = new FilteredElementCollector(_doc, _view.Id)
            .WhereElementIsNotElementType()
            .OfCategoryId(category.Id)
            .FirstOrDefault();

        // Confirmed live (user-reported): a parameter this element genuinely
        // has and can read (e.g. "Familie"/Family) can still be structurally
        // unusable in a ParameterFilterElement rule — Revit rejected it with
        // "One of the given rules refers to a parameter that does not apply
        // to this filter's categories." at Apply time, on every single value.
        // GetFilterableParametersInCommon is Revit's own authoritative answer
        // for which parameter ids work in a filter rule for this category —
        // but "Familie" stayed on that list (it genuinely can be filtered on,
        // just not this way) and the error persisted after gating on it
        // alone, which is what exposed the real second half of this: this
        // tool always builds a STRING equals-rule from AsValueString() text
        // (Command.cs ApplyFilters — ParameterFilterRuleFactory.CreateEqualsRule
        // (ElementId, string)), but Family/Type/Level/Workset/Material-style
        // identity parameters are StorageType.ElementId under the hood — they
        // only DISPLAY as text; the rule Revit needs for them takes an actual
        // target ElementId, not a string, so a string-typed rule against one
        // is rejected regardless of category. Rather than half-build ElementId
        // rule support (resolving each legend value's string back to its real
        // target element correctly for every possible referenced type is a
        // meaningfully bigger feature, not a bug fix), StorageType.ElementId
        // parameters are excluded here the same way GetFilterableParametersInCommon
        // already excludes structurally-unusable ones — String/Integer/Double
        // parameters, which this tool's rule-building already handles
        // correctly, aren't affected.
        var filterableIds = sample is null
            ? new HashSet<ElementId>()
            : ParameterFilterUtilities.GetFilterableParametersInCommon(_doc, new List<ElementId> { category.Id }).ToHashSet();

        var names = sample is null
            ? new List<string>()
            : sample.Parameters.Cast<Parameter>()
                .Where(p => filterableIds.Contains(p.Id) && p.StorageType != StorageType.ElementId)
                .Select(p => p.Definition.Name).Distinct().OrderBy(n => n).ToList();

        _paramBox.Items.Clear();
        _paramBox.Items.AddRange(names.Cast<object>().ToArray());
        if (_paramBox.Items.Count > 0) _paramBox.SelectedIndex = 0;
        else RefreshLegend();
    }

    private void RefreshLegend()
    {
        _legendPanel.Children.Clear();
        _legendRows.Clear();

        if (SelectedCategoryLabel() is not { } categoryName || _paramBox.SelectedItem is not string paramName || _paletteBox.SelectedItem is not string paletteName)
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
            var box = OttawaWorkUi.CheckBoxItem(value, isChecked: true);
            box.FontStyle = isNoValue ? System.Windows.FontStyles.Italic : System.Windows.FontStyles.Normal;
            box.Click += (_, _) => RefreshLegendSummary();
            var countText = new TextBlock { Text = count.ToString(), FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), HorizontalAlignment = HorizontalAlignment.Right, Width = 40 };
            row.Children.Add(swatch);
            row.Children.Add(box);
            row.Children.Add(countText);
            _legendPanel.Children.Add(row);
            _legendRows[value] = (color, count, box);
        }

        if (byValue.Count == 0)
            _legendPanel.Children.Add(new TextBlock { Text = "No elements of this category are in the active view.", Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), FontSize = 12, TextWrapping = TextWrapping.Wrap });

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
        SelectedCategory = SelectedCategoryLabel() is { } name ? _categoriesByName[name] : null;
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
