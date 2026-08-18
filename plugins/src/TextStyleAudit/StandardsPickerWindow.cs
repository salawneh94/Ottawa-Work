using StackPanel = System.Windows.Controls.StackPanel;
using CheckBox = System.Windows.Controls.CheckBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

using BIMFlow.Shared;

namespace BIMFlow.TextStyleAudit;

/// <summary>
/// Approved text/dimension type checklist — dark themed, replacing the old
/// WinForms CheckedListBox dialog. Everything is checked (approved) by
/// default; unchecking a type flags every instance using it.
/// </summary>
public class StandardsPickerWindow : BimFlowWindow
{
    private readonly List<CheckBox> _checkBoxes = new();
    private readonly List<string> _typeNames;

    public HashSet<string> ApprovedTypeNames { get; private set; } = new();

    public StandardsPickerWindow(List<string> usedTypeNames) : base("BIMFlow — Text Style Audit", minWidth: 380)
    {
        _typeNames = usedTypeNames;

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🔤", "Text Style Audit", "Check every text/dimension type on your standard. Unchecked types are flagged as off-standard."));

        var checklistStack = new StackPanel();
        foreach (var name in usedTypeNames)
        {
            var checkBox = BimFlowUi.CheckBoxItem(name, isChecked: true);
            checklistStack.Children.Add(checkBox);
            _checkBoxes.Add(checkBox);
        }
        var scroll = new ScrollViewer { MaxHeight = 340, Content = checklistStack };
        root.Children.Add(BimFlowUi.Card(scroll, padding: 8));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var runButton = BimFlowUi.PrimaryButton("Run audit");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        runButton.Click += (_, _) =>
        {
            ApprovedTypeNames = _checkBoxes
                .Zip(_typeNames, (checkBox, name) => (checkBox.IsChecked == true, name))
                .Where(t => t.Item1)
                .Select(t => t.name)
                .ToHashSet();
            DialogResult = true;
            Close();
        };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(runButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }
}
