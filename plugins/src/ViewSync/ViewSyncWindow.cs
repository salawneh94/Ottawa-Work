using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.ViewSync;

/// <summary>View template picker + a view checklist — dark themed, replacing the old WinForms ListBox/CheckedListBox dialog.</summary>
public class ViewSyncWindow : BimFlowWindow
{
    private readonly ComboBox _templateBox = BimFlowUi.ComboBox();
    private readonly List<View> _templates;
    private readonly List<(CheckBox CheckBox, View View)> _viewChecks = new();

    public View? SelectedTemplate { get; private set; }
    public List<View> SelectedViews { get; private set; } = new();

    public ViewSyncWindow(List<View> templates, List<View> candidateViews) : base("BIMFlow — ViewSync", minWidth: 420)
    {
        _templates = templates;

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🔄", "View Sync", "Apply one view template to a set of views in one pass."));

        var templateStack = new StackPanel();
        templateStack.Children.Add(BimFlowUi.FieldLabel("View template to apply"));
        foreach (var t in templates) _templateBox.Items.Add(t.Name);
        if (_templateBox.Items.Count > 0) _templateBox.SelectedIndex = 0;
        templateStack.Children.Add(_templateBox);
        root.Children.Add(BimFlowUi.Card(templateStack));

        var viewsStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        viewsStack.Children.Add(BimFlowUi.SectionHeader("Apply to these view(s)"));
        var checklistStack = new StackPanel();
        foreach (var view in candidateViews)
        {
            var checkBox = BimFlowUi.CheckBoxItem(view.Name);
            checklistStack.Children.Add(checkBox);
            _viewChecks.Add((checkBox, view));
        }
        var scroll = new ScrollViewer { MaxHeight = 300, Content = checklistStack };
        viewsStack.Children.Add(BimFlowUi.Card(scroll, padding: 8));
        root.Children.Add(viewsStack);

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
        SelectedTemplate = _templateBox.SelectedIndex >= 0 ? _templates[_templateBox.SelectedIndex] : null;
        SelectedViews = _viewChecks.Where(t => t.CheckBox.IsChecked == true).Select(t => t.View).ToList();
        DialogResult = true;
        Close();
    }
}
