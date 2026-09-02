using StackPanel = System.Windows.Controls.StackPanel;
using CheckBox = System.Windows.Controls.CheckBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

/// <summary>Reusable "pick one or more target views" checklist dialog — dark themed, replacing the old WinForms CheckedListBox dialog.</summary>
public class ViewPickerForm : OttawaWorkWindow
{
    private readonly List<(CheckBox CheckBox, View View)> _checks = new();

    public List<View> SelectedViews { get; private set; } = new();

    public ViewPickerForm(string title, string instructions, List<View> candidateViews) : base(title, minWidth: 380)
    {
        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("👁️", title, instructions, Close));

        var checklistStack = new StackPanel();
        foreach (var view in candidateViews)
        {
            var checkBox = OttawaWorkUi.CheckBoxItem(view.Name);
            checklistStack.Children.Add(checkBox);
            _checks.Add((checkBox, view));
        }
        var scroll = new ScrollViewer { MaxHeight = 340, Content = checklistStack };
        root.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var allButton = OttawaWorkUi.SecondaryButton("Select all");
        var okButton = OttawaWorkUi.PrimaryButton("OK");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        allButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        allButton.Click += (_, _) => { foreach (var (checkBox, _) in _checks) checkBox.IsChecked = true; };
        okButton.Click += (_, _) =>
        {
            SelectedViews = _checks.Where(t => t.CheckBox.IsChecked == true).Select(t => t.View).ToList();
            DialogResult = true;
            Close();
        };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(allButton);
        buttonRow.Children.Add(okButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }
}
