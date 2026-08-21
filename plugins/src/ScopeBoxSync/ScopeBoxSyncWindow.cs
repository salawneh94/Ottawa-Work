using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.ScopeBoxSync;

/// <summary>Scope box picker + a view checklist — dark themed, replacing the old WinForms ListBox/CheckedListBox dialog.</summary>
public class ScopeBoxSyncWindow : OttawaWorkWindow
{
    private readonly ComboBox _scopeBoxBox = OttawaWorkUi.ComboBox();
    private readonly List<Element> _scopeBoxes;
    private readonly List<(CheckBox CheckBox, View View)> _viewChecks = new();

    public Element? SelectedScopeBox { get; private set; }
    public List<View> SelectedViews { get; private set; } = new();

    public ScopeBoxSyncWindow(List<Element> scopeBoxes, List<View> candidateViews) : base("Ottawa Tools — Scope Box Sync", minWidth: 420)
    {
        _scopeBoxes = scopeBoxes;

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("📦", "Scope Box Sync", "Apply one scope box to a set of views in one pass."));

        var scopeStack = new StackPanel();
        scopeStack.Children.Add(OttawaWorkUi.FieldLabel("Scope box to apply"));
        foreach (var box in scopeBoxes) _scopeBoxBox.Items.Add(box.Name);
        if (_scopeBoxBox.Items.Count > 0) _scopeBoxBox.SelectedIndex = 0;
        scopeStack.Children.Add(_scopeBoxBox);
        root.Children.Add(OttawaWorkUi.Card(scopeStack));

        var viewsStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        viewsStack.Children.Add(OttawaWorkUi.SectionHeader("Apply to these view(s)"));
        var checklistStack = new StackPanel();
        foreach (var view in candidateViews)
        {
            var checkBox = OttawaWorkUi.CheckBoxItem(view.Name);
            checklistStack.Children.Add(checkBox);
            _viewChecks.Add((checkBox, view));
        }
        var scroll = new ScrollViewer { MaxHeight = 280, Content = checklistStack };
        viewsStack.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));
        root.Children.Add(viewsStack);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var applyButton = OttawaWorkUi.PrimaryButton("Apply");
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
        SelectedScopeBox = _scopeBoxBox.SelectedIndex >= 0 ? _scopeBoxes[_scopeBoxBox.SelectedIndex] : null;
        SelectedViews = _viewChecks.Where(t => t.CheckBox.IsChecked == true).Select(t => t.View).ToList();
        DialogResult = true;
        Close();
    }
}
