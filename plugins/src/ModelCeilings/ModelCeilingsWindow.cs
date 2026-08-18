using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.ModelCeilings;

/// <summary>Ceiling type + height offset picker — dark themed, replacing the old WinForms TableLayoutPanel dialog.</summary>
public class ModelCeilingsWindow : BimFlowWindow
{
    private readonly ComboBox _typeBox = BimFlowUi.ComboBox();
    private readonly TextBox _heightBox = BimFlowUi.TextBox();
    private readonly List<CeilingType> _types;

    public CeilingType? SelectedType { get; private set; }

    /// <summary>Revit's internal length unit is always feet regardless of project/display units — this stays in feet for the direct parameter Set() call in Command.cs, converted from the user-facing meters field on submit.</summary>
    public double HeightOffsetFeet { get; private set; } = 9;

    public ModelCeilingsWindow(List<CeilingType> types, int roomCount) : base("BIMFlow — ModelCeilings", minWidth: 360)
    {
        _types = types;
        _heightBox.Text = "2.7";

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🏠", "Model Ceilings", $"{roomCount} room(s) in the active view."));

        var fieldsStack = new StackPanel();
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Ceiling type"));
        foreach (var t in types) _typeBox.Items.Add(t.Name);
        if (_typeBox.Items.Count > 0) _typeBox.SelectedIndex = 0;
        fieldsStack.Children.Add(_typeBox);
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Height above level (m)"));
        fieldsStack.Children.Add(_heightBox);
        root.Children.Add(BimFlowUi.Card(fieldsStack));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var createButton = BimFlowUi.PrimaryButton("Create ceilings");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        createButton.Click += CreateButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(createButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedType = _typeBox.SelectedIndex >= 0 ? _types[_typeBox.SelectedIndex] : null;
        var heightMeters = double.TryParse(_heightBox.Text, out var h) ? h : 2.7;
        HeightOffsetFeet = UnitUtils.ConvertToInternalUnits(heightMeters, UnitTypeId.Meters);
        DialogResult = true;
        Close();
    }
}
