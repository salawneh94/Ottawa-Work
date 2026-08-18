using StackPanel = System.Windows.Controls.StackPanel;
using CheckBox = System.Windows.Controls.CheckBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

using BIMFlow.Shared;

namespace BIMFlow.TagAll;

/// <summary>Category checklist — dark themed, replacing the old WinForms CheckedListBox dialog.</summary>
public class TagAllWindow : BimFlowWindow
{
    private readonly List<(CheckBox CheckBox, Autodesk.Revit.DB.BuiltInCategory Category)> _checks = new();

    public static readonly (string Label, Autodesk.Revit.DB.BuiltInCategory Category)[] Categories =
    {
        ("Doors", Autodesk.Revit.DB.BuiltInCategory.OST_Doors),
        ("Windows", Autodesk.Revit.DB.BuiltInCategory.OST_Windows),
        ("Rooms", Autodesk.Revit.DB.BuiltInCategory.OST_Rooms),
        ("Furniture", Autodesk.Revit.DB.BuiltInCategory.OST_Furniture),
        ("Structural Columns", Autodesk.Revit.DB.BuiltInCategory.OST_StructuralColumns),
        ("Structural Framing", Autodesk.Revit.DB.BuiltInCategory.OST_StructuralFraming),
        ("Mechanical Equipment", Autodesk.Revit.DB.BuiltInCategory.OST_MechanicalEquipment),
        ("Plumbing Fixtures", Autodesk.Revit.DB.BuiltInCategory.OST_PlumbingFixtures),
    };

    public List<Autodesk.Revit.DB.BuiltInCategory> SelectedCategories { get; private set; } = new();

    public TagAllWindow() : base("BIMFlow — TagAll+", minWidth: 360)
    {
        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🏷️", "TagAll+", "Tag every untagged element of these categories in the active view."));

        var checklistStack = new StackPanel();
        foreach (var (label, category) in Categories)
        {
            var checkBox = BimFlowUi.CheckBoxItem(label);
            checklistStack.Children.Add(checkBox);
            _checks.Add((checkBox, category));
        }
        var scroll = new ScrollViewer { MaxHeight = 320, Content = checklistStack };
        root.Children.Add(BimFlowUi.Card(scroll, padding: 8));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var tagButton = BimFlowUi.PrimaryButton("Tag all");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        tagButton.Click += (_, _) =>
        {
            SelectedCategories = _checks.Where(t => t.CheckBox.IsChecked == true).Select(t => t.Category).ToList();
            DialogResult = true;
            Close();
        };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(tagButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }
}
