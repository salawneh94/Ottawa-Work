using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using OttawaWork.Shared;

namespace OttawaWork.RoomTagger;

/// <summary>
/// Category checklist plus the two target parameter names — dark themed,
/// same shape as RoomInventoryWindow. Categories default to the set most
/// likely to be missing Revit's automatic Room field (MEP equipment,
/// fixtures, and the line-based systems — ducts, pipes, cable trays,
/// conduits — that don't get one at all). Result read from
/// SelectedCategories, RoomNumberParameter, RoomNameParameter after
/// ShowDialog() returns true.
/// </summary>
public class RoomTaggerWindow : OttawaWorkWindow
{
    private readonly List<CheckBox> _checkBoxes = new();
    private readonly TextBox _numberParamBox = OttawaWorkUi.TextBox();
    private readonly TextBox _nameParamBox = OttawaWorkUi.TextBox();

    public static readonly (string Label, Autodesk.Revit.DB.BuiltInCategory Category)[] Categories =
    {
        ("Mechanical Equipment", Autodesk.Revit.DB.BuiltInCategory.OST_MechanicalEquipment),
        ("Plumbing Fixtures", Autodesk.Revit.DB.BuiltInCategory.OST_PlumbingFixtures),
        ("Electrical Equipment", Autodesk.Revit.DB.BuiltInCategory.OST_ElectricalEquipment),
        ("Electrical Fixtures", Autodesk.Revit.DB.BuiltInCategory.OST_ElectricalFixtures),
        ("Lighting Fixtures", Autodesk.Revit.DB.BuiltInCategory.OST_LightingFixtures),
        ("Air Terminals", Autodesk.Revit.DB.BuiltInCategory.OST_DuctTerminal),
        ("Sprinklers", Autodesk.Revit.DB.BuiltInCategory.OST_Sprinklers),
        ("Specialty Equipment", Autodesk.Revit.DB.BuiltInCategory.OST_SpecialityEquipment),
        ("Ducts", Autodesk.Revit.DB.BuiltInCategory.OST_DuctCurves),
        ("Pipes", Autodesk.Revit.DB.BuiltInCategory.OST_PipeCurves),
        ("Cable Trays", Autodesk.Revit.DB.BuiltInCategory.OST_CableTray),
        ("Conduits", Autodesk.Revit.DB.BuiltInCategory.OST_Conduit),
    };

    public List<Autodesk.Revit.DB.BuiltInCategory> SelectedCategories { get; private set; } = new();
    public string RoomNumberParameter { get; private set; } = "";
    public string RoomNameParameter { get; private set; } = "";

    public RoomTaggerWindow() : base("Ottawa Tools — RoomTagger")
    {
        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar(
            "🏷️",
            "Room Tagger",
            "Write each room's number/name onto every element found inside it — for categories Revit doesn't auto-tag with a Room field."));

        root.Children.Add(OttawaWorkUi.SectionHeader("Categories to tag"));
        var listStack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        foreach (var (label, _) in Categories)
        {
            var checkBox = OttawaWorkUi.CheckBoxItem(label);
            _checkBoxes.Add(checkBox);
            listStack.Children.Add(checkBox);
        }
        root.Children.Add(OttawaWorkUi.Card(listStack));

        root.Children.Add(OttawaWorkUi.SectionHeader("Target parameters"));
        var paramStack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        paramStack.Children.Add(OttawaWorkUi.FieldLabel("Room number parameter (must already exist on these categories — leave blank to skip)"));
        _numberParamBox.Text = "Room Number";
        paramStack.Children.Add(_numberParamBox);
        paramStack.Children.Add(OttawaWorkUi.FieldLabel("Room name parameter (must already exist on these categories — leave blank to skip)"));
        _nameParamBox.Text = "Room Name";
        paramStack.Children.Add(_nameParamBox);
        root.Children.Add(OttawaWorkUi.Card(paramStack));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var runButton = OttawaWorkUi.PrimaryButton("Tag elements");
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
        RoomNumberParameter = _numberParamBox.Text.Trim();
        RoomNameParameter = _nameParamBox.Text.Trim();
        DialogResult = true;
        Close();
    }
}
