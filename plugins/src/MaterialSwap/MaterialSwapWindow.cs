using ComboBox = System.Windows.Controls.ComboBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.MaterialSwap;

/// <summary>Pick a "from" and "to" material — dark themed, replacing the old WinForms TableLayoutPanel dialog.</summary>
public class MaterialSwapWindow : BimFlowWindow
{
    private readonly ComboBox _fromBox = BimFlowUi.ComboBox();
    private readonly ComboBox _toBox = BimFlowUi.ComboBox();

    public Material? FromMaterial { get; private set; }
    public Material? ToMaterial { get; private set; }

    public MaterialSwapWindow(List<Material> materials) : base("BIMFlow — MaterialSwap")
    {
        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🎨", "Material Swap", "Reassign a material across every wall/floor/roof/ceiling type layer that uses it."));

        var fieldsStack = new StackPanel();
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Swap from"));
        fieldsStack.Children.Add(_fromBox);
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Swap to"));
        fieldsStack.Children.Add(_toBox);
        root.Children.Add(BimFlowUi.Card(fieldsStack));

        foreach (var m in materials)
        {
            _fromBox.Items.Add(new MaterialItem(m));
            _toBox.Items.Add(new MaterialItem(m));
        }
        if (_fromBox.Items.Count > 0) _fromBox.SelectedIndex = 0;
        if (_toBox.Items.Count > 1) _toBox.SelectedIndex = 1;

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var swapButton = BimFlowUi.PrimaryButton("Swap");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        swapButton.Click += SwapButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(swapButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void SwapButton_Click(object sender, RoutedEventArgs e)
    {
        FromMaterial = (_fromBox.SelectedItem as MaterialItem)?.Material;
        ToMaterial = (_toBox.SelectedItem as MaterialItem)?.Material;
        DialogResult = true;
        Close();
    }

    private record MaterialItem(Material Material)
    {
        public override string ToString() => Material.Name;
    }
}
