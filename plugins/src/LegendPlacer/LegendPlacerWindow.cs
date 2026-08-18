using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.LegendPlacer;

/// <summary>
/// Legend picker + X/Y position fields + a checklist of target sheets —
/// dark themed, replacing the old WinForms ListBox/CheckedListBox dialog.
/// </summary>
public class LegendPlacerWindow : BimFlowWindow
{
    private readonly ComboBox _legendBox = BimFlowUi.ComboBox();
    private readonly TextBox _xBox = BimFlowUi.TextBox();
    private readonly TextBox _yBox = BimFlowUi.TextBox();
    private readonly List<View> _legends;
    private readonly List<(CheckBox CheckBox, ViewSheet Sheet)> _sheetChecks = new();

    public View? SelectedLegend { get; private set; }
    public List<ViewSheet> SelectedSheets { get; private set; } = new();
    public XYZ Position { get; private set; } = XYZ.Zero;

    public LegendPlacerWindow(List<View> legends, List<ViewSheet> sheets) : base("BIMFlow — LegendPlacer", minWidth: 420)
    {
        _legends = legends;
        _xBox.Text = "0.6";
        _yBox.Text = "0.6";

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🗂️", "Legend Placer", "Batch-place one legend view onto many sheets at the same position."));

        var legendStack = new StackPanel();
        legendStack.Children.Add(BimFlowUi.FieldLabel("Legend to place"));
        foreach (var legend in legends) _legendBox.Items.Add(legend.Name);
        if (_legendBox.Items.Count > 0) _legendBox.SelectedIndex = 0;
        legendStack.Children.Add(_legendBox);

        var posRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var xStack = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        xStack.Children.Add(BimFlowUi.FieldLabel("Position X (m)"));
        _xBox.Width = 100;
        xStack.Children.Add(_xBox);
        var yStack = new StackPanel();
        yStack.Children.Add(BimFlowUi.FieldLabel("Position Y (m)"));
        _yBox.Width = 100;
        yStack.Children.Add(_yBox);
        posRow.Children.Add(xStack);
        posRow.Children.Add(yStack);
        legendStack.Children.Add(posRow);
        root.Children.Add(BimFlowUi.Card(legendStack));

        var sheetsStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        sheetsStack.Children.Add(BimFlowUi.SectionHeader("Target sheets"));
        var checklistStack = new StackPanel();
        foreach (var sheet in sheets)
        {
            var checkBox = BimFlowUi.CheckBoxItem($"{sheet.SheetNumber} - {sheet.Name}");
            checklistStack.Children.Add(checkBox);
            _sheetChecks.Add((checkBox, sheet));
        }
        var scroll = new ScrollViewer { MaxHeight = 260, Content = checklistStack };
        sheetsStack.Children.Add(BimFlowUi.Card(scroll, padding: 8));
        root.Children.Add(sheetsStack);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var placeButton = BimFlowUi.PrimaryButton("Place");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        placeButton.Click += PlaceButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(placeButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void PlaceButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedLegend = _legendBox.SelectedIndex >= 0 ? _legends[_legendBox.SelectedIndex] : null;
        SelectedSheets = _sheetChecks.Where(t => t.CheckBox.IsChecked == true).Select(t => t.Sheet).ToList();
        var xMeters = double.TryParse(_xBox.Text, out var xv) ? xv : 0.6;
        var yMeters = double.TryParse(_yBox.Text, out var yv) ? yv : 0.6;
        var x = UnitUtils.ConvertToInternalUnits(xMeters, UnitTypeId.Meters);
        var y = UnitUtils.ConvertToInternalUnits(yMeters, UnitTypeId.Meters);
        Position = new XYZ(x, y, 0);
        DialogResult = true;
        Close();
    }
}
