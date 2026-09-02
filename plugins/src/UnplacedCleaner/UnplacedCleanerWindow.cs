using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using CheckBox = System.Windows.Controls.CheckBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using OttawaWork.Shared;

namespace OttawaWork.UnplacedCleaner;

/// <summary>Checklist of unplaced rooms — all checked by default, since every row already matched the "never placed" filter.</summary>
public class UnplacedCleanerWindow : OttawaWorkWindow
{
    private readonly List<(CheckBox CheckBox, ElementId Id)> _checks = new();

    public List<ElementId> SelectedElementIdsToDelete { get; private set; } = new();

    public UnplacedCleanerWindow(List<Room> rooms) : base("Ottawa Tools — Unplaced Cleaner", minWidth: 460)
    {
        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🧹", "Unplaced Cleaner", $"{rooms.Count} unplaced room(s) found — never placed on a plan.", Close));

        var rowsStack = new StackPanel();
        foreach (var room in rooms)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            var number = string.IsNullOrWhiteSpace(room.Number) ? "(no number)" : room.Number;
            var name = string.IsNullOrWhiteSpace(room.Name) ? "(unnamed)" : room.Name;
            var levelName = room.Level?.Name ?? "(no level)";
            var checkBox = OttawaWorkUi.CheckBoxItem($"{number}  {name}", isChecked: true);
            checkBox.Width = 260;
            row.Children.Add(checkBox);
            row.Children.Add(new TextBlock { Text = levelName, FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), VerticalAlignment = System.Windows.VerticalAlignment.Center });
            rowsStack.Children.Add(row);
            _checks.Add((checkBox, room.Id));
        }

        var scroll = new ScrollViewer { MaxHeight = 380, Content = rowsStack };
        root.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));

        var toggleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var allButton = OttawaWorkUi.SecondaryButton("All");
        var noneButton = OttawaWorkUi.SecondaryButton("None");
        allButton.Margin = new Thickness(0, 0, 8, 0);
        allButton.Click += (_, _) => { foreach (var c in _checks) c.CheckBox.IsChecked = true; };
        noneButton.Click += (_, _) => { foreach (var c in _checks) c.CheckBox.IsChecked = false; };
        toggleRow.Children.Add(allButton);
        toggleRow.Children.Add(noneButton);
        root.Children.Add(toggleRow);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var deleteButton = OttawaWorkUi.DangerButton("Delete checked rooms");
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
