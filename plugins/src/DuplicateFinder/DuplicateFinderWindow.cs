using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using CheckBox = System.Windows.Controls.CheckBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.DuplicateFinder;

/// <summary>
/// A flat checklist grouped by duplicate group — dark themed, replacing the
/// old WinForms checkable TreeView (WPF has no built-in checkable tree).
/// The first element in each group is unchecked (kept) by default, the
/// rest checked (flagged for deletion), matching the old behavior.
/// </summary>
public class DuplicateFinderWindow : BimFlowWindow
{
    private readonly List<(CheckBox CheckBox, ElementId Id)> _checks = new();

    public List<ElementId> SelectedElementIdsToDelete { get; private set; } = new();

    public DuplicateFinderWindow(List<DuplicateGroup> groups) : base("BIMFlow — Duplicate Finder", minWidth: 460)
    {
        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🧬", "Duplicate Finder", $"{groups.Count} duplicate group(s) found. The first element in each group is kept by default."));

        var groupsStack = new StackPanel();
        foreach (var group in groups)
        {
            var groupStack = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            var header = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            header.Children.Add(BimFlowUi.SectionHeader($"{group.CategoryName} — {group.TypeName}"));
            header.Children.Add(BimFlowUi.Badge($"{group.Elements.Count} instances"));
            groupStack.Children.Add(header);

            for (var i = 0; i < group.Elements.Count; i++)
            {
                var element = group.Elements[i];
                var label = $"Element id {element.Id}" + (i == 0 ? "  (kept)" : "");
                var checkBox = BimFlowUi.CheckBoxItem(label, isChecked: i > 0);
                groupStack.Children.Add(checkBox);
                _checks.Add((checkBox, element.Id));
            }

            groupsStack.Children.Add(BimFlowUi.Card(groupStack));
        }
        var scroll = new ScrollViewer { MaxHeight = 380, Content = groupsStack };
        root.Children.Add(scroll);

        var buttonRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var deleteButton = BimFlowUi.PrimaryButton("Delete checked elements");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        deleteButton.Click += (_, _) =>
        {
            SelectedElementIdsToDelete = _checks.Where(t => t.CheckBox.IsChecked == true).Select(t => t.Id).ToList();
            DialogResult = true;
            Close();
        };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(deleteButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }
}
