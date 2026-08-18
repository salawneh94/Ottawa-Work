using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using BIMFlow.Shared;

namespace BIMFlow.RoomInventory;

/// <summary>
/// Category checklist for the per-room inventory scan — dark themed,
/// replacing the old WinForms CheckedListBox dialog. Same categories and
/// default selection as before; the result is read from
/// <see cref="SelectedCategories"/> after <see cref="ShowDialog"/> returns
/// true.
/// </summary>
public class RoomInventoryWindow : BimFlowWindow
{
    private readonly List<CheckBox> _checkBoxes = new();

    public static readonly (string Label, Autodesk.Revit.DB.BuiltInCategory Category)[] Categories =
    {
        ("Furniture", Autodesk.Revit.DB.BuiltInCategory.OST_Furniture),
        ("Furniture Systems", Autodesk.Revit.DB.BuiltInCategory.OST_FurnitureSystems),
        ("Casework", Autodesk.Revit.DB.BuiltInCategory.OST_Casework),
        ("Mechanical Equipment", Autodesk.Revit.DB.BuiltInCategory.OST_MechanicalEquipment),
        ("Plumbing Fixtures", Autodesk.Revit.DB.BuiltInCategory.OST_PlumbingFixtures),
        ("Electrical Fixtures", Autodesk.Revit.DB.BuiltInCategory.OST_ElectricalFixtures),
        ("Lighting Fixtures", Autodesk.Revit.DB.BuiltInCategory.OST_LightingFixtures),
        ("Specialty Equipment", Autodesk.Revit.DB.BuiltInCategory.OST_SpecialityEquipment),
    };

    public List<Autodesk.Revit.DB.BuiltInCategory> SelectedCategories { get; private set; } = new();

    public RoomInventoryWindow() : base("BIMFlow — RoomInventory")
    {
        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("📦", "Room Inventory", "Count elements of the selected categories inside every placed room."));
        root.Children.Add(BimFlowUi.SectionHeader("Categories to inventory"));

        var listStack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        for (var i = 0; i < Categories.Length; i++)
        {
            var checkBox = BimFlowUi.CheckBoxItem(Categories[i].Label, isChecked: i < 3);
            _checkBoxes.Add(checkBox);
            listStack.Children.Add(checkBox);
        }
        root.Children.Add(BimFlowUi.Card(listStack));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var runButton = BimFlowUi.PrimaryButton("Run inventory");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        runButton.Click += RunButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(runButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedCategories = _checkBoxes
            .Zip(Categories, (checkBox, category) => (checkBox.IsChecked == true, category.Category))
            .Where(t => t.Item1)
            .Select(t => t.Category)
            .ToList();
        DialogResult = true;
        Close();
    }
}
