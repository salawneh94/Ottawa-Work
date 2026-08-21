using ListBox = System.Windows.Controls.ListBox;
using StackPanel = System.Windows.Controls.StackPanel;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.Excel2Revit;

/// <summary>Pick a schedule to export — dark themed, replacing the old WinForms ListBox dialog.</summary>
public class SchedulePickerWindow : OttawaWorkWindow
{
    private readonly ListBox _list = new() { MaxHeight = 320 };
    private readonly List<ViewSchedule> _schedules;

    public ViewSchedule? SelectedSchedule { get; private set; }

    public SchedulePickerWindow(List<ViewSchedule> schedules) : base("Ottawa Tools — Excel2Revit", minWidth: 340)
    {
        _schedules = schedules;

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.FieldLabel("Schedule to export"));

        foreach (var schedule in schedules) _list.Items.Add(schedule.Name);
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        _list.Background = OttawaWorkUi.BrushOf(OttawaWorkUi.CardBackgroundAlt);
        _list.Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary);
        _list.BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.BorderColor);
        root.Children.Add(OttawaWorkUi.Card(_list, padding: 4));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var nextButton = OttawaWorkUi.PrimaryButton("Next");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        nextButton.Click += NextButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(nextButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedSchedule = _list.SelectedIndex >= 0 ? _schedules[_list.SelectedIndex] : null;
        DialogResult = true;
        Close();
    }
}
