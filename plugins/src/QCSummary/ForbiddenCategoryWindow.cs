using StackPanel = System.Windows.Controls.StackPanel;
using CheckBox = System.Windows.Controls.CheckBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.QCSummary;

/// <summary>Optional "forbidden category" checklist run before the QC audit — dark themed, replacing the old WinForms CheckedListBox dialog.</summary>
public class ForbiddenCategoryWindow : BimFlowWindow
{
    private readonly List<(CheckBox CheckBox, Category Category)> _checks = new();

    public List<Category> ForbiddenCategories { get; private set; } = new();

    public ForbiddenCategoryWindow(List<Category> categories) : base("BIMFlow — QCSummary: Forbidden Categories", minWidth: 380)
    {
        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🚫", "Forbidden Categories", "Check any categories that shouldn't appear in this model (optional)."));

        var checklistStack = new StackPanel();
        foreach (var category in categories)
        {
            var checkBox = BimFlowUi.CheckBoxItem(category.Name);
            checklistStack.Children.Add(checkBox);
            _checks.Add((checkBox, category));
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
            ForbiddenCategories = _checks.Where(t => t.CheckBox.IsChecked == true).Select(t => t.Category).ToList();
            DialogResult = true;
            Close();
        };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(runButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }
}
