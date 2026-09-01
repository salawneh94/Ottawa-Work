using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using RadioButton = System.Windows.Controls.RadioButton;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using Visibility = System.Windows.Visibility;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.LegendPlacer;

/// <summary>
/// Legend picker + a position method (copy the exact spot from a sheet that
/// already has it placed, or anchor to a title block corner with an inset
/// offset) + a searchable target-sheet checklist that shows which sheets
/// already have the legend placed (disabled, can't double-place there).
/// Dark themed, replacing the old WinForms ListBox/CheckedListBox dialog.
/// </summary>
public class LegendPlacerWindow : OttawaWorkWindow
{
    private readonly Document _doc;
    private readonly List<View> _legends;
    private readonly List<ViewSheet> _allSheets;

    private readonly ComboBox _legendBox = OttawaWorkUi.ComboBox();

    private readonly RadioButton _copyFromRefRadio;
    private readonly RadioButton _anchorCornerRadio;
    private readonly StackPanel _referencePanel = new();
    private readonly StackPanel _cornerPanel = new();
    private readonly ComboBox _referenceSheetBox = OttawaWorkUi.ComboBox();
    private readonly TextBlock _referencePositionLabel = new() { FontSize = 10, Margin = new Thickness(0, 2, 0, 0) };
    private readonly ComboBox _cornerBox = OttawaWorkUi.ComboBox();
    private readonly TextBox _offsetXBox = OttawaWorkUi.TextBox();
    private readonly TextBox _offsetYBox = OttawaWorkUi.TextBox();
    private List<ElementId> _referenceSheetIds = new();

    private readonly TextBox _searchBox = OttawaWorkUi.TextBox();
    private readonly StackPanel _sheetListPanel = new();
    private readonly TextBlock _footerText = new() { FontSize = 10, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 6, 0, 0) };
    private readonly List<SheetRow> _rows = new();

    private Dictionary<ElementId, Viewport> _existingPlacements = new();

    public View? SelectedLegend { get; private set; }
    public List<ViewSheet> SelectedSheets { get; private set; } = new();
    public LegendPositionMethod PositionMethod { get; private set; } = LegendPositionMethod.AnchorToTitleBlockCorner;
    public ElementId? ReferenceSheetId { get; private set; }
    public LegendCorner Corner { get; private set; } = LegendCorner.BottomRight;
    public double OffsetXFeet { get; private set; }
    public double OffsetYFeet { get; private set; }

    private sealed record SheetRow(ViewSheet Sheet, CheckBox Box);

    public LegendPlacerWindow(Document doc, List<View> legends, List<ViewSheet> sheets) : base("Ottawa Tools — Legend Placer", minWidth: 640)
    {
        _doc = doc;
        _legends = legends;
        _allSheets = sheets;

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🗂️", "Legend Placer", "Batch-place one legend view onto many sheets at a consistent position."));

        var legendStack = new StackPanel();
        legendStack.Children.Add(OttawaWorkUi.FieldLabel("Legend to place"));
        foreach (var legend in legends) _legendBox.Items.Add(legend.Name);
        if (_legendBox.Items.Count > 0) _legendBox.SelectedIndex = 0;
        _legendBox.SelectionChanged += (_, _) => OnLegendChanged();
        legendStack.Children.Add(_legendBox);
        root.Children.Add(OttawaWorkUi.Card(legendStack));

        root.Children.Add(OttawaWorkUi.SectionHeader("Position method"));
        var methodStack = new StackPanel();

        _copyFromRefRadio = OttawaWorkUi.RadioButtonItem("Copy position from reference sheet", "positionMethod", isChecked: false);
        _anchorCornerRadio = OttawaWorkUi.RadioButtonItem("Anchor to title block corner", "positionMethod", isChecked: true);
        _copyFromRefRadio.Checked += (_, _) => SetPositionMethod(LegendPositionMethod.CopyFromReferenceSheet);
        _anchorCornerRadio.Checked += (_, _) => SetPositionMethod(LegendPositionMethod.AnchorToTitleBlockCorner);
        methodStack.Children.Add(_copyFromRefRadio);

        _referencePanel.Margin = new Thickness(22, 4, 0, 10);
        _referencePanel.Children.Add(OttawaWorkUi.FieldLabel("Reference sheet (already has this legend placed)"));
        _referenceSheetBox.SelectionChanged += (_, _) => RefreshReferencePositionLabel();
        _referencePanel.Children.Add(_referenceSheetBox);
        _referencePositionLabel.Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary);
        _referencePanel.Children.Add(_referencePositionLabel);
        methodStack.Children.Add(_referencePanel);

        methodStack.Children.Add(_anchorCornerRadio);
        _cornerPanel.Margin = new Thickness(22, 4, 0, 0);
        var cornerRow = new StackPanel { Orientation = Orientation.Horizontal };
        var cornerStack = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        cornerStack.Children.Add(OttawaWorkUi.FieldLabel("Corner"));
        _cornerBox.Items.AddRange(new object[] { "Bottom-Right", "Bottom-Left", "Top-Right", "Top-Left" });
        _cornerBox.SelectedIndex = 0;
        cornerStack.Children.Add(_cornerBox);
        cornerRow.Children.Add(cornerStack);
        var xStack = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        xStack.Children.Add(OttawaWorkUi.FieldLabel("X (mm)"));
        _offsetXBox.Text = "20";
        _offsetXBox.Width = 80;
        xStack.Children.Add(_offsetXBox);
        cornerRow.Children.Add(xStack);
        var yStack = new StackPanel();
        yStack.Children.Add(OttawaWorkUi.FieldLabel("Y (mm)"));
        _offsetYBox.Text = "20";
        _offsetYBox.Width = 80;
        yStack.Children.Add(_offsetYBox);
        cornerRow.Children.Add(yStack);
        _cornerPanel.Children.Add(cornerRow);
        methodStack.Children.Add(_cornerPanel);

        root.Children.Add(OttawaWorkUi.Card(methodStack));

        var sheetsStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var sheetsHeaderRow = new StackPanel { Orientation = Orientation.Horizontal };
        sheetsHeaderRow.Children.Add(OttawaWorkUi.SectionHeader("Target sheets"));
        sheetsStack.Children.Add(sheetsHeaderRow);

        var quickRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var allButton = OttawaWorkUi.SecondaryButton("All");
        var noneButton = OttawaWorkUi.SecondaryButton("None");
        allButton.Margin = new Thickness(0, 0, 6, 0);
        allButton.Click += (_, _) => { foreach (var row in _rows.Where(r => r.Box.IsEnabled)) row.Box.IsChecked = true; RefreshFooter(); };
        noneButton.Click += (_, _) => { foreach (var row in _rows.Where(r => r.Box.IsEnabled)) row.Box.IsChecked = false; RefreshFooter(); };
        quickRow.Children.Add(allButton);
        quickRow.Children.Add(noneButton);
        sheetsStack.Children.Add(quickRow);

        _searchBox.Margin = new Thickness(0, 0, 0, 8);
        _searchBox.ToolTip = "Search sheets by number or name...";
        _searchBox.TextChanged += (_, _) => ApplySearchFilter();
        sheetsStack.Children.Add(_searchBox);

        var scroll = new ScrollViewer { MaxHeight = 260, Content = _sheetListPanel };
        sheetsStack.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));
        sheetsStack.Children.Add(_footerText);
        root.Children.Add(sheetsStack);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var placeButton = OttawaWorkUi.PrimaryButton("Place");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        placeButton.Click += PlaceButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(placeButton);
        root.Children.Add(buttonRow);

        SetContent(root);

        SetPositionMethod(LegendPositionMethod.AnchorToTitleBlockCorner);
        OnLegendChanged();
    }

    private void OnLegendChanged()
    {
        var legend = _legendBox.SelectedIndex >= 0 ? _legends[_legendBox.SelectedIndex] : null;
        _existingPlacements = legend is not null ? LegendPlacerEngine.ExistingPlacements(_doc, legend.Id) : new();

        _referenceSheetBox.Items.Clear();
        _referenceSheetIds = new List<ElementId>();
        foreach (var sheetId in _existingPlacements.Keys)
        {
            if (_doc.GetElement(sheetId) is not ViewSheet sheet) continue;
            _referenceSheetBox.Items.Add($"{sheet.SheetNumber} - {sheet.Name}");
            _referenceSheetIds.Add(sheetId);
        }

        var hasReference = _referenceSheetIds.Count > 0;
        _copyFromRefRadio.IsEnabled = hasReference;
        if (hasReference)
            _referenceSheetBox.SelectedIndex = 0;
        else if (_copyFromRefRadio.IsChecked == true)
            _anchorCornerRadio.IsChecked = true;
        RefreshReferencePositionLabel();

        RefreshSheetList();
    }

    private void RefreshReferencePositionLabel()
    {
        if (_referenceSheetBox.SelectedIndex < 0 || _referenceSheetBox.SelectedIndex >= _referenceSheetIds.Count)
        {
            _referencePositionLabel.Text = "";
            return;
        }

        var sheetId = _referenceSheetIds[_referenceSheetBox.SelectedIndex];
        if (!_existingPlacements.TryGetValue(sheetId, out var viewport))
        {
            _referencePositionLabel.Text = "";
            return;
        }

        var center = viewport.GetBoxCenter();
        var xMm = UnitUtils.ConvertFromInternalUnits(center.X, UnitTypeId.Millimeters);
        var yMm = UnitUtils.ConvertFromInternalUnits(center.Y, UnitTypeId.Millimeters);
        _referencePositionLabel.Text = $"Legend found at X: {xMm:0.0} mm, Y: {yMm:0.0} mm";
    }

    private void SetPositionMethod(LegendPositionMethod method)
    {
        _referencePanel.Visibility = method == LegendPositionMethod.CopyFromReferenceSheet ? Visibility.Visible : Visibility.Collapsed;
        _cornerPanel.Visibility = method == LegendPositionMethod.AnchorToTitleBlockCorner ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshSheetList()
    {
        _sheetListPanel.Children.Clear();
        _rows.Clear();

        foreach (var sheet in _allSheets)
        {
            var alreadyPlaced = _existingPlacements.ContainsKey(sheet.Id);
            var label = $"{sheet.SheetNumber} - {sheet.Name}" + (alreadyPlaced ? "  (already placed)" : "");
            var checkbox = OttawaWorkUi.CheckBoxItem(label);
            checkbox.IsEnabled = !alreadyPlaced;
            checkbox.Click += (_, _) => RefreshFooter();

            _sheetListPanel.Children.Add(checkbox);
            _rows.Add(new SheetRow(sheet, checkbox));
        }

        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        var search = _searchBox.Text?.Trim() ?? "";
        foreach (var row in _rows)
        {
            var matches = string.IsNullOrEmpty(search)
                || row.Sheet.SheetNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.Sheet.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
            row.Box.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
        }
        RefreshFooter();
    }

    private void RefreshFooter()
    {
        var selectable = _rows.Where(r => r.Box.IsEnabled).ToList();
        var selectedCount = selectable.Count(r => r.Box.IsChecked == true);
        _footerText.Text = $"{selectedCount}/{selectable.Count} selectable sheet(s) chosen · {_existingPlacements.Count} already placed";
    }

    private void PlaceButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedLegend = _legendBox.SelectedIndex >= 0 ? _legends[_legendBox.SelectedIndex] : null;
        SelectedSheets = _rows.Where(r => r.Box.IsEnabled && r.Box.IsChecked == true).Select(r => r.Sheet).ToList();

        PositionMethod = _copyFromRefRadio.IsChecked == true ? LegendPositionMethod.CopyFromReferenceSheet : LegendPositionMethod.AnchorToTitleBlockCorner;
        ReferenceSheetId = _referenceSheetBox.SelectedIndex >= 0 && _referenceSheetBox.SelectedIndex < _referenceSheetIds.Count
            ? _referenceSheetIds[_referenceSheetBox.SelectedIndex]
            : null;

        Corner = (_cornerBox.SelectedItem as string) switch
        {
            "Bottom-Left" => LegendCorner.BottomLeft,
            "Top-Right" => LegendCorner.TopRight,
            "Top-Left" => LegendCorner.TopLeft,
            _ => LegendCorner.BottomRight,
        };

        var xMm = double.TryParse(_offsetXBox.Text, out var xv) ? xv : 20.0;
        var yMm = double.TryParse(_offsetYBox.Text, out var yv) ? yv : 20.0;
        OffsetXFeet = UnitUtils.ConvertToInternalUnits(xMm, UnitTypeId.Millimeters);
        OffsetYFeet = UnitUtils.ConvertToInternalUnits(yMm, UnitTypeId.Millimeters);

        DialogResult = true;
        Close();
    }
}
