using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.InsulationManager;

/// <summary>Insulation type + thickness picker — dark themed, replacing the old WinForms TableLayoutPanel dialog.</summary>
public class InsulationManagerWindow : BimFlowWindow
{
    private readonly ComboBox _typeBox = BimFlowUi.ComboBox();
    private readonly TextBox _thicknessBox = BimFlowUi.TextBox();
    private readonly List<ElementType> _types;

    public ElementType? SelectedType { get; private set; }

    /// <summary>Kept in inches — Command.cs converts this straight to feet for the parameter Set() call — converted from the user-facing millimeters field on submit.</summary>
    public double ThicknessInches { get; private set; } = 1;

    public InsulationManagerWindow(List<ElementType> types, int elementCount) : base("BIMFlow — InsulationManager", minWidth: 360)
    {
        _types = types;
        _thicknessBox.Text = "25";

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🧊", "Insulation Manager", $"{elementCount} pipe/duct element(s) selected."));

        var fieldsStack = new StackPanel();
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Insulation type"));
        foreach (var t in types) _typeBox.Items.Add(t.Name);
        if (_typeBox.Items.Count > 0) _typeBox.SelectedIndex = 0;
        fieldsStack.Children.Add(_typeBox);
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Thickness (mm)"));
        fieldsStack.Children.Add(_thicknessBox);
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
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedType = _typeBox.SelectedIndex >= 0 ? _types[_typeBox.SelectedIndex] : null;
        var thicknessMm = double.TryParse(_thicknessBox.Text, out var t) && t > 0 ? t : 25;
        ThicknessInches = UnitUtils.Convert(thicknessMm, UnitTypeId.Millimeters, UnitTypeId.Inches);
        DialogResult = true;
        Close();
    }
}
