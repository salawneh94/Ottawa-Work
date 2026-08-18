using StackPanel = System.Windows.Controls.StackPanel;
using CheckBox = System.Windows.Controls.CheckBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Thickness = System.Windows.Thickness;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.GridBubbleManager;

/// <summary>
/// Datum-type and bubble-end options + a checklist of views to apply them
/// to — dark themed, replacing the old WinForms CheckedListBox dialog.
/// </summary>
public class GridBubbleOptionsWindow : BimFlowWindow
{
    private readonly CheckBox _includeGrids = BimFlowUi.CheckBoxItem("Grids", isChecked: true);
    private readonly CheckBox _includeLevels = BimFlowUi.CheckBoxItem("Levels", isChecked: true);
    private readonly CheckBox _showStart = BimFlowUi.CheckBoxItem("Show bubble at start (End 0)", isChecked: true);
    private readonly CheckBox _showEnd = BimFlowUi.CheckBoxItem("Show bubble at end (End 1)", isChecked: false);
    private readonly List<(CheckBox CheckBox, View View)> _viewChecks = new();

    public List<View> SelectedViews { get; private set; } = new();
    public bool IncludeGrids { get; private set; }
    public bool IncludeLevels { get; private set; }
    public bool ShowStartBubble { get; private set; }
    public bool ShowEndBubble { get; private set; }

    public GridBubbleOptionsWindow(View activeView, List<View> candidateViews) : base("BIMFlow — Grid Bubble Manager", minWidth: 420)
    {
        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🔘", "Grid Bubble Manager", "Show or hide grid/level bubble ends across a set of views."));

        var optionsStack = new StackPanel();
        optionsStack.Children.Add(BimFlowUi.SectionHeader("Datum types"));
        optionsStack.Children.Add(_includeGrids);
        optionsStack.Children.Add(_includeLevels);
        optionsStack.Children.Add(BimFlowUi.SectionHeader("Bubble ends", BimFlowUi.Accent));
        optionsStack.Children.Add(_showStart);
        optionsStack.Children.Add(_showEnd);
        root.Children.Add(BimFlowUi.Card(optionsStack));

        var listStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        listStack.Children.Add(BimFlowUi.SectionHeader("Apply to these view(s)"));
        var checklistStack = new StackPanel();
        foreach (var view in candidateViews)
        {
            var checkBox = BimFlowUi.CheckBoxItem(view.Name, isChecked: view.Id == activeView.Id);
            checklistStack.Children.Add(checkBox);
            _viewChecks.Add((checkBox, view));
        }
        var scroll = new ScrollViewer { MaxHeight = 260, Content = checklistStack };
        listStack.Children.Add(BimFlowUi.Card(scroll, padding: 8));
        root.Children.Add(listStack);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var applyButton = BimFlowUi.PrimaryButton("Apply");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        applyButton.Click += (_, _) =>
        {
            SelectedViews = _viewChecks.Where(t => t.CheckBox.IsChecked == true).Select(t => t.View).ToList();
            IncludeGrids = _includeGrids.IsChecked == true;
            IncludeLevels = _includeLevels.IsChecked == true;
            ShowStartBubble = _showStart.IsChecked == true;
            ShowEndBubble = _showEnd.IsChecked == true;
            DialogResult = true;
            Close();
        };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(applyButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }
}
