using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using OttawaWork.Shared;

namespace OttawaWork.TitleBlockUpdater;

/// <summary>Parameter + new value picker — dark themed, replacing the old WinForms TableLayoutPanel dialog.</summary>
public class TitleBlockUpdaterWindow : OttawaWorkWindow
{
    private readonly ComboBox _paramBox = OttawaWorkUi.ComboBox();
    private readonly TextBox _valueBox = OttawaWorkUi.TextBox();

    public string? SelectedParameterName { get; private set; }
    public string NewValue { get; private set; } = "";

    public TitleBlockUpdaterWindow(List<string> parameterNames, int instanceCount) : base("Ottawa Tools — TitleBlockUpdater", minWidth: 380)
    {
        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🖼️", "Title Block Updater", $"{instanceCount} title block instance(s) in this project.", Close));

        var fieldsStack = new StackPanel();
        fieldsStack.Children.Add(OttawaWorkUi.FieldLabel("Parameter"));
        _paramBox.Items.AddRange(parameterNames.Cast<object>().ToArray());
        if (_paramBox.Items.Count > 0) _paramBox.SelectedIndex = 0;
        fieldsStack.Children.Add(_paramBox);
        fieldsStack.Children.Add(OttawaWorkUi.FieldLabel("New value"));
        fieldsStack.Children.Add(_valueBox);
        root.Children.Add(OttawaWorkUi.Card(fieldsStack));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var applyButton = OttawaWorkUi.PrimaryButton("Apply to all");
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
        SelectedParameterName = _paramBox.SelectedItem as string;
        NewValue = _valueBox.Text;
        DialogResult = true;
        Close();
    }
}
