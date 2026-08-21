using ListBox = System.Windows.Controls.ListBox;
using StackPanel = System.Windows.Controls.StackPanel;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

namespace OttawaWork.Shared;

/// <summary>Generic single-choice-from-a-list dialog — for the many "pick one of these strings" prompts across plugins.</summary>
public class SimplePickerDialog : OttawaWorkWindow
{
    private readonly ListBox _list = new() { MaxHeight = 320 };

    public string? SelectedText { get; private set; }

    public SimplePickerDialog(string title, string label, List<string> options) : base(title, minWidth: 340)
    {
        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.FieldLabel(label));

        foreach (var option in options) _list.Items.Add(option);
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        _list.Background = OttawaWorkUi.BrushOf(OttawaWorkUi.CardBackgroundAlt);
        _list.Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary);
        _list.BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.BorderColor);
        root.Children.Add(OttawaWorkUi.Card(_list, padding: 4));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var okButton = OttawaWorkUi.PrimaryButton("OK");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        okButton.Click += OkButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(okButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedText = _list.SelectedItem as string;
        DialogResult = true;
        Close();
    }
}
