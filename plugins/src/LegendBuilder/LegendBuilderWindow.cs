using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Slider = System.Windows.Controls.Slider;
using Button = System.Windows.Controls.Button;
using Border = System.Windows.Controls.Border;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Canvas = System.Windows.Controls.Canvas;
using Rectangle = System.Windows.Shapes.Rectangle;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Thickness = System.Windows.Thickness;
using CornerRadius = System.Windows.CornerRadius;
using FontWeights = System.Windows.FontWeights;
using TextTrimming = System.Windows.TextTrimming;
using TextWrapping = System.Windows.TextWrapping;
using Cursors = System.Windows.Input.Cursors;
using Brushes = System.Windows.Media.Brushes;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.LegendBuilder;

/// <summary>
/// Category + parameter picker with a live value list (count per value,
/// click a value then click a palette color to assign it), a style panel
/// (title, size preset, fine-tune sliders, placement offset, options), and
/// a live WPF-rendered preview of the legend before it's actually built —
/// only Generate & Place writes anything to the model, everything here is
/// just collecting intent, same as every other dialog in this codebase.
/// </summary>
public class LegendBuilderWindow : OttawaWorkWindow
{
    private readonly Document _doc;
    private readonly Dictionary<string, Category> _categoriesByName = new();
    private readonly Dictionary<string, Color> _colorByValue = new();
    private string? _selectedValueForColor;

    private readonly ComboBox _categoryBox = OttawaWorkUi.ComboBox();
    private readonly ComboBox _paramBox = OttawaWorkUi.ComboBox();
    private readonly StackPanel _valuesPanel = new();
    private readonly TextBlock _scanCaption = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly List<(string Value, int Count, Border Swatch, StackPanel Row)> _valueRows = new();

    private readonly TextBox _titleBox = OttawaWorkUi.TextBox();
    private readonly Button _compactButton;
    private readonly Button _standardButton;
    private readonly Button _largeButton;

    private readonly Slider _textSlider = new() { Minimum = 1.0, Maximum = 5.0, Value = 2.4 };
    private readonly Slider _rowSlider = new() { Minimum = 5.0, Maximum = 20.0, Value = 10.0 };
    private readonly Slider _swatchSlider = new() { Minimum = 6.0, Maximum = 30.0, Value = 18.0 };
    private readonly Slider _paddingSlider = new() { Minimum = 0.0, Maximum = 5.0, Value = 1.5 };
    private readonly Slider _placementRightSlider = new() { Minimum = 0.0, Maximum = 50.0, Value = 10.0 };
    private readonly Slider _placementUpSlider = new() { Minimum = 0.0, Maximum = 50.0, Value = 10.0 };

    private readonly TextBlock _textReadout = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 44 };
    private readonly TextBlock _rowReadout = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 44 };
    private readonly TextBlock _swatchReadout = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 44 };
    private readonly TextBlock _paddingReadout = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 44 };
    private readonly TextBlock _rightReadout = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 44 };
    private readonly TextBlock _upReadout = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 44 };

    private readonly CheckBox _showCountBox = OttawaWorkUi.CheckBoxItem("Show Count", isChecked: true);
    private readonly CheckBox _headerFillBox = OttawaWorkUi.CheckBoxItem("Header Fill", isChecked: true);
    private readonly CheckBox _altRowsBox = OttawaWorkUi.CheckBoxItem("Alt Rows", isChecked: true);

    private readonly Canvas _previewCanvas = new() { Width = 260, Height = 300, ClipToBounds = true };
    private readonly TextBlock _previewCaption = new() { FontSize = 9, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 6, 0, 0) };

    public Category? SelectedCategory { get; private set; }
    public string? SelectedParameterName { get; private set; }
    public List<LegendValueRow> Rows { get; private set; } = new();
    // Named LegendStyle, not Style — a bare "Style" here would hide the
    // inherited FrameworkElement.Style property (CS0108), confirmed live
    // (compiler-flagged) once this window's first real WPF compile ran in
    // CI (this sandbox can't compile WPF locally to catch it earlier).
    public LegendStyleOptions LegendStyle { get; private set; } = null!;

    public LegendBuilderWindow(Document doc) : base("Ottawa Tools — Legend Builder", minWidth: 900)
    {
        _doc = doc;

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("📗", "Legend Builder", "Scan a category, assign colors, then generate a color legend.", Close));

        var columns = new StackPanel { Orientation = Orientation.Horizontal };

        // ---- Values column ----
        var valuesCol = new StackPanel { Width = 280, Margin = new Thickness(0, 0, 14, 0) };
        var valuesHeaderRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        valuesHeaderRow.Children.Add(OttawaWorkUi.SectionHeader("Values"));
        var autoAssignButton = OttawaWorkUi.SecondaryButton("Auto-assign");
        autoAssignButton.Margin = new Thickness(60, -6, 0, 10);
        autoAssignButton.Click += (_, _) => { AutoAssignColors(); RefreshValuesDisplay(); RefreshPreview(); };
        valuesCol.Children.Add(valuesHeaderRow);
        valuesCol.Children.Add(autoAssignButton);

        valuesCol.Children.Add(OttawaWorkUi.FieldLabel("Category"));
        foreach (var (label, builtIn) in CommonCategories.Roster)
        {
            var category = Category.GetCategory(doc, builtIn);
            if (category is null) continue;
            _categoriesByName[label] = category;
            _categoryBox.Items.Add(label);
        }
        _categoryBox.SelectionChanged += (_, _) => RefreshParameterOptions();
        valuesCol.Children.Add(_categoryBox);

        valuesCol.Children.Add(OttawaWorkUi.FieldLabel("Parameter"));
        _paramBox.SelectionChanged += (_, _) => RefreshValues();
        valuesCol.Children.Add(_paramBox);

        var valuesScroll = new ScrollViewer { MaxHeight = 220, Content = _valuesPanel };
        valuesCol.Children.Add(OttawaWorkUi.Card(valuesScroll, padding: 8));
        valuesCol.Children.Add(new TextBlock { Text = "Pick a value, then click a color", FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 8, 0, 6) });

        var paletteGrid = new StackPanel();
        var paletteRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var paletteRow2 = new StackPanel { Orientation = Orientation.Horizontal };
        var colors = ColorPalette.Vivid;
        for (var i = 0; i < colors.Length; i++)
        {
            var swatch = PaletteSwatch(colors[i]);
            (i < 6 ? paletteRow1 : paletteRow2).Children.Add(swatch);
        }
        paletteGrid.Children.Add(paletteRow1);
        paletteGrid.Children.Add(paletteRow2);
        valuesCol.Children.Add(paletteGrid);

        var resetButton = OttawaWorkUi.SecondaryButton("Reset");
        resetButton.Margin = new Thickness(0, 8, 0, 0);
        resetButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        resetButton.Click += (_, _) => { AutoAssignColors(); RefreshValuesDisplay(); RefreshPreview(); };
        valuesCol.Children.Add(resetButton);

        columns.Children.Add(valuesCol);

        // ---- Style column ----
        var styleCol = new StackPanel { Width = 260, Margin = new Thickness(0, 0, 14, 0) };
        styleCol.Children.Add(OttawaWorkUi.SectionHeader("Style"));

        styleCol.Children.Add(OttawaWorkUi.FieldLabel("Title"));
        _titleBox.Text = "Legend";
        _titleBox.TextChanged += (_, _) => RefreshPreview();
        styleCol.Children.Add(_titleBox);

        styleCol.Children.Add(OttawaWorkUi.FieldLabel("Size"));
        var sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        _compactButton = OttawaWorkUi.SecondaryButton("Compact");
        _standardButton = OttawaWorkUi.SecondaryButton("Standard");
        _largeButton = OttawaWorkUi.SecondaryButton("Large");
        _compactButton.Margin = new Thickness(0, 0, 4, 0);
        _standardButton.Margin = new Thickness(0, 0, 4, 0);
        _compactButton.Click += (_, _) => ApplySizePreset(LegendSizePreset.Compact);
        _standardButton.Click += (_, _) => ApplySizePreset(LegendSizePreset.Standard);
        _largeButton.Click += (_, _) => ApplySizePreset(LegendSizePreset.Large);
        sizeRow.Children.Add(_compactButton);
        sizeRow.Children.Add(_standardButton);
        sizeRow.Children.Add(_largeButton);
        styleCol.Children.Add(sizeRow);

        styleCol.Children.Add(OttawaWorkUi.SectionHeader("Fine-tune"));
        styleCol.Children.Add(SliderRow("Text", _textSlider, _textReadout));
        styleCol.Children.Add(SliderRow("Row H.", _rowSlider, _rowReadout));
        styleCol.Children.Add(SliderRow("Swatch", _swatchSlider, _swatchReadout));
        styleCol.Children.Add(SliderRow("Padding", _paddingSlider, _paddingReadout));

        styleCol.Children.Add(OttawaWorkUi.SectionHeader("Placement"));
        styleCol.Children.Add(SliderRow("Right", _placementRightSlider, _rightReadout));
        styleCol.Children.Add(SliderRow("Up", _placementUpSlider, _upReadout));

        styleCol.Children.Add(OttawaWorkUi.SectionHeader("Options"));
        _showCountBox.Click += (_, _) => RefreshPreview();
        _headerFillBox.Click += (_, _) => RefreshPreview();
        _altRowsBox.Click += (_, _) => RefreshPreview();
        styleCol.Children.Add(_showCountBox);
        styleCol.Children.Add(_headerFillBox);
        styleCol.Children.Add(_altRowsBox);

        columns.Children.Add(styleCol);

        // ---- Preview column ----
        var previewCol = new StackPanel { Width = 280 };
        previewCol.Children.Add(OttawaWorkUi.SectionHeader("Legend Preview"));
        previewCol.Children.Add(OttawaWorkUi.Card(_previewCanvas, padding: 8));
        previewCol.Children.Add(_previewCaption);
        previewCol.Children.Add(_scanCaption);

        var generateButton = OttawaWorkUi.PrimaryButton("Generate & Place Legend");
        generateButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        generateButton.Margin = new Thickness(0, 14, 0, 0);
        generateButton.Click += (_, _) => Finish();
        previewCol.Children.Add(generateButton);

        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        cancelButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        cancelButton.Margin = new Thickness(0, 8, 0, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        previewCol.Children.Add(cancelButton);

        columns.Children.Add(previewCol);

        root.Children.Add(columns);
        SetContent(root);

        foreach (var slider in new[] { _textSlider, _rowSlider, _swatchSlider, _paddingSlider, _placementRightSlider, _placementUpSlider })
            slider.ValueChanged += (_, _) => { RefreshReadouts(); RefreshPreview(); };
        RefreshReadouts();

        if (_categoryBox.Items.Count > 0) _categoryBox.SelectedIndex = 0;
        RefreshParameterOptions();
    }

    private static StackPanel SliderRow(string label, Slider slider, TextBlock readout)
    {
        slider.Width = 150;
        slider.Margin = new Thickness(0, 0, 6, 0);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 46, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(slider);
        row.Children.Add(readout);
        return row;
    }

    private void RefreshReadouts()
    {
        _textReadout.Text = $"{_textSlider.Value:0.0}mm";
        _rowReadout.Text = $"{_rowSlider.Value:0}mm";
        _swatchReadout.Text = $"{_swatchSlider.Value:0}mm";
        _paddingReadout.Text = $"{_paddingSlider.Value:0.0}mm";
        _rightReadout.Text = $"{_placementRightSlider.Value:0}mm";
        _upReadout.Text = $"{_placementUpSlider.Value:0}mm";
    }

    private void ApplySizePreset(LegendSizePreset preset)
    {
        var (swatchMm, rowMm, textMm, paddingMm) = LegendBuilderEngine.SizePreset(preset);
        _swatchSlider.Value = swatchMm;
        _rowSlider.Value = rowMm;
        _textSlider.Value = textMm;
        _paddingSlider.Value = paddingMm;
        OttawaWorkUi.SetToggleActive(_compactButton, preset == LegendSizePreset.Compact);
        OttawaWorkUi.SetToggleActive(_standardButton, preset == LegendSizePreset.Standard);
        OttawaWorkUi.SetToggleActive(_largeButton, preset == LegendSizePreset.Large);
    }

    private Border PaletteSwatch(Color color)
    {
        var swatch = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(5),
            Margin = new Thickness(0, 0, 4, 0),
            Background = ToBrush(color),
            Cursor = Cursors.Hand,
        };
        swatch.MouseLeftButtonDown += (_, _) => AssignColorToSelected(color);
        return swatch;
    }

    private void AssignColorToSelected(Color color)
    {
        if (_selectedValueForColor is not { } value) return;
        _colorByValue[value] = color;
        var row = _valueRows.FirstOrDefault(r => r.Value == value);
        if (row.Swatch is not null) row.Swatch.Background = ToBrush(color);
        RefreshPreview();
    }

    private void SelectValueForColor(string value)
    {
        _selectedValueForColor = value;
        foreach (var r in _valueRows)
            r.Row.Background = r.Value == value ? OttawaWorkUi.BrushOf(OttawaWorkUi.AccentSoft) : Brushes.Transparent;
    }

    private void RefreshParameterOptions()
    {
        _paramBox.Items.Clear();
        if (_categoryBox.SelectedItem is not string categoryName || !_categoriesByName.TryGetValue(categoryName, out var category))
        {
            RefreshValues();
            return;
        }

        var names = LegendBuilderEngine.ParameterNames(_doc, category);
        _paramBox.Items.AddRange(names.Cast<object>());
        if (_paramBox.Items.Count > 0) _paramBox.SelectedIndex = 0;
        else RefreshValues();
    }

    private void RefreshValues()
    {
        _valueRows.Clear();

        if (_categoryBox.SelectedItem is not string categoryName || !_categoriesByName.TryGetValue(categoryName, out var category)
            || _paramBox.SelectedItem is not string paramName)
        {
            _scanCaption.Text = "";
            RefreshValuesDisplay();
            RefreshPreview();
            return;
        }

        var scanned = LegendBuilderEngine.ScanValues(_doc, category, paramName);
        _scanCaption.Text = $"Scanned {category.Name} — {scanned.Count} unique \"{paramName}\" value(s) found.";
        foreach (var (value, count) in scanned)
            _valueRows.Add((value, count, new Border(), new StackPanel()));

        AutoAssignColors();
        RefreshValuesDisplay();
        RefreshPreview();
    }

    private void AutoAssignColors()
    {
        _colorByValue.Clear();
        for (var i = 0; i < _valueRows.Count; i++)
            _colorByValue[_valueRows[i].Value] = ColorPalette.ForIndex(i);
    }

    private void RefreshValuesDisplay()
    {
        _valuesPanel.Children.Clear();
        var rebuilt = new List<(string Value, int Count, Border Swatch, StackPanel Row)>();

        foreach (var (value, count, _, _) in _valueRows)
        {
            var color = _colorByValue.GetValueOrDefault(value, ColorPalette.ForIndex(0));
            var swatch = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 8, 0), Background = ToBrush(color), VerticalAlignment = VerticalAlignment.Center };
            var label = new TextBlock { Text = value, FontSize = 12, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), Width = 150, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            var countText = new TextBlock { Text = count.ToString(), FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), HorizontalAlignment = HorizontalAlignment.Right, Width = 30, VerticalAlignment = VerticalAlignment.Center };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 3, 4, 3), Cursor = Cursors.Hand };
            row.Children.Add(swatch);
            row.Children.Add(label);
            row.Children.Add(countText);

            var capturedValue = value;
            row.MouseLeftButtonDown += (_, _) => SelectValueForColor(capturedValue);

            _valuesPanel.Children.Add(row);
            rebuilt.Add((value, count, swatch, row));
        }

        _valueRows.Clear();
        _valueRows.AddRange(rebuilt);

        if (_valueRows.Count == 0)
            _valuesPanel.Children.Add(new TextBlock { Text = "No values found for this category/parameter.", FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) });
    }

    private void RefreshPreview()
    {
        _previewCanvas.Children.Clear();

        const double scale = 2.0;
        var swatchPx = _swatchSlider.Value * scale;
        var rowPx = _rowSlider.Value * scale;
        var paddingPx = _paddingSlider.Value * scale;
        var canvasWidth = _previewCanvas.Width - 8;

        double y = 6;

        if (!string.IsNullOrWhiteSpace(_titleBox.Text))
        {
            if (_headerFillBox.IsChecked == true)
            {
                var headerRect = new Rectangle { Width = canvasWidth, Height = rowPx * 1.3, Fill = OttawaWorkUi.BrushOf(OttawaWorkUi.Accent), RadiusX = 3, RadiusY = 3 };
                Canvas.SetLeft(headerRect, 4);
                Canvas.SetTop(headerRect, y);
                _previewCanvas.Children.Add(headerRect);
            }
            var titleText = new TextBlock { Text = _titleBox.Text, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary) };
            Canvas.SetLeft(titleText, 10);
            Canvas.SetTop(titleText, y + rowPx * 0.3);
            _previewCanvas.Children.Add(titleText);
            y += rowPx * 1.3 + 4;
        }

        for (var i = 0; i < _valueRows.Count; i++)
        {
            var (value, count, _, _) = _valueRows[i];
            if (y > _previewCanvas.Height - rowPx) break;

            if (_altRowsBox.IsChecked == true && i % 2 == 1)
            {
                var altRect = new Rectangle { Width = canvasWidth, Height = rowPx, Fill = OttawaWorkUi.BrushOf(OttawaWorkUi.CardBackgroundAlt) };
                Canvas.SetLeft(altRect, 4);
                Canvas.SetTop(altRect, y);
                _previewCanvas.Children.Add(altRect);
            }

            var color = _colorByValue.GetValueOrDefault(value, ColorPalette.ForIndex(0));
            var swatchRect = new Rectangle { Width = swatchPx, Height = swatchPx * 0.7, Fill = ToBrush(color), RadiusX = 2, RadiusY = 2 };
            Canvas.SetLeft(swatchRect, 8);
            Canvas.SetTop(swatchRect, y + (rowPx - swatchPx * 0.7) / 2);
            _previewCanvas.Children.Add(swatchRect);

            var labelText = _showCountBox.IsChecked == true ? $"{value}  ({count})" : value;
            var label = new TextBlock { Text = labelText, FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), TextTrimming = TextTrimming.CharacterEllipsis, Width = canvasWidth - swatchPx - paddingPx - 12 };
            Canvas.SetLeft(label, 8 + swatchPx + paddingPx);
            Canvas.SetTop(label, y + rowPx / 2 - 7);
            _previewCanvas.Children.Add(label);

            y += rowPx;
        }

        _previewCaption.Text = $"Placed at Right {_placementRightSlider.Value:0}mm · Up {_placementUpSlider.Value:0}mm · Swatch {_swatchSlider.Value:0}mm · Row {_rowSlider.Value:0}mm";
    }

    private static System.Windows.Media.SolidColorBrush ToBrush(Color color) =>
        new(System.Windows.Media.Color.FromRgb(color.Red, color.Green, color.Blue));

    private void Finish()
    {
        SelectedCategory = _categoryBox.SelectedItem is string name && _categoriesByName.TryGetValue(name, out var cat) ? cat : null;
        SelectedParameterName = _paramBox.SelectedItem as string;
        Rows = _valueRows.Select(r => new LegendValueRow(r.Value, r.Count, _colorByValue.GetValueOrDefault(r.Value, ColorPalette.ForIndex(0)))).ToList();
        LegendStyle = new LegendStyleOptions(
            _titleBox.Text,
            _textSlider.Value,
            _rowSlider.Value,
            _swatchSlider.Value,
            _paddingSlider.Value,
            _placementRightSlider.Value,
            _placementUpSlider.Value,
            _showCountBox.IsChecked == true,
            _headerFillBox.IsChecked == true,
            _altRowsBox.IsChecked == true);

        if (SelectedCategory is null || SelectedParameterName is null || Rows.Count == 0)
        {
            System.Windows.MessageBox.Show("Pick a category and parameter with at least one scanned value first.", "Ottawa Tools — Legend Builder");
            return;
        }

        DialogResult = true;
        Close();
    }
}
