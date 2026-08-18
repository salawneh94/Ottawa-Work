using StackPanel = System.Windows.Controls.StackPanel;
using CheckBox = System.Windows.Controls.CheckBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.CrossProjectTransfer;

/// <summary>Multi-select checklist of element types — dark themed, replacing the old WinForms CheckedListBox dialog.</summary>
public class TypeCheckListWindow : BimFlowWindow
{
    private readonly List<(CheckBox CheckBox, ElementType Type)> _checks = new();

    public List<ElementId> SelectedTypeIds { get; private set; } = new();

    public TypeCheckListWindow(string title, List<ElementType> types) : base(title, minWidth: 380)
    {
        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("📋", title, $"{types.Count} type(s) — check the ones to copy."));

        var checklistStack = new StackPanel();
        foreach (var type in types)
        {
            var label = type.FamilyName is { Length: > 0 } fam ? $"{fam}: {type.Name}" : type.Name;
            var checkBox = BimFlowUi.CheckBoxItem(label);
            checklistStack.Children.Add(checkBox);
            _checks.Add((checkBox, type));
        }
        var scroll = new ScrollViewer { MaxHeight = 340, Content = checklistStack };
        root.Children.Add(BimFlowUi.Card(scroll, padding: 8));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var allButton = BimFlowUi.SecondaryButton("Select all");
        var copyButton = BimFlowUi.PrimaryButton("Copy selected");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        allButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        allButton.Click += (_, _) => { foreach (var (checkBox, _) in _checks) checkBox.IsChecked = true; };
        copyButton.Click += (_, _) =>
        {
            SelectedTypeIds = _checks.Where(t => t.CheckBox.IsChecked == true).Select(t => t.Type.Id).ToList();
            DialogResult = true;
            Close();
        };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(allButton);
        buttonRow.Children.Add(copyButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }
}
