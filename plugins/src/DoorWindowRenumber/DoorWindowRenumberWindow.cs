using StackPanel = System.Windows.Controls.StackPanel;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using RadioButton = System.Windows.Controls.RadioButton;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using TextWrapping = System.Windows.TextWrapping;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using BIMFlow.Shared;

namespace BIMFlow.DoorWindowRenumber;

/// <summary>
/// Scope (doors vs. windows, only asked when nothing is pre-selected) +
/// prefix + start number + increment — dark themed, replacing the old
/// WinForms TableLayoutPanel dialog. WPF has no built-in NumericUpDown, so
/// start number/increment are plain text boxes parsed as ints.
/// </summary>
public class DoorWindowRenumberWindow : BimFlowWindow
{
    private readonly RadioButton? _windowsRadio;
    private readonly TextBox _prefixBox = BimFlowUi.TextBox();
    private readonly TextBox _startBox = BimFlowUi.TextBox();
    private readonly TextBox _incrementBox = BimFlowUi.TextBox();

    public bool UseWindowsCategory { get; private set; }
    public string Prefix { get; private set; } = "";
    public int StartNumber { get; private set; } = 1;
    public int Increment { get; private set; } = 1;

    public DoorWindowRenumberWindow(int selectionCount, bool categoryPickerNeeded) : base("BIMFlow — Door/Window Renumber", minWidth: 380)
    {
        _startBox.Text = "1";
        _incrementBox.Text = "1";

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🔢", "Door/Window Renumber", "Renumber the Mark parameter in reading order, with a prefix and increment."));

        var scopeText = categoryPickerNeeded
            ? "No doors or windows are selected — pick a category to renumber every instance in the model:"
            : $"{selectionCount} selected door(s)/window(s) will be renumbered.";
        var scopeStack = new StackPanel();
        scopeStack.Children.Add(new TextBlock
        {
            Text = scopeText,
            FontSize = 12,
            Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, categoryPickerNeeded ? 10 : 0),
        });

        if (categoryPickerNeeded)
        {
            var choiceRow = new StackPanel { Orientation = Orientation.Horizontal };
            var doorsRadio = BimFlowUi.RadioButtonItem("All doors", "scope", isChecked: true);
            _windowsRadio = BimFlowUi.RadioButtonItem("All windows", "scope");
            choiceRow.Children.Add(doorsRadio);
            choiceRow.Children.Add(_windowsRadio);
            scopeStack.Children.Add(choiceRow);
        }
        root.Children.Add(BimFlowUi.Card(scopeStack));

        var fieldsStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Prefix"));
        fieldsStack.Children.Add(_prefixBox);
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Start number"));
        fieldsStack.Children.Add(_startBox);
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Increment"));
        fieldsStack.Children.Add(_incrementBox);
        root.Children.Add(BimFlowUi.Card(fieldsStack));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var renumberButton = BimFlowUi.PrimaryButton("Renumber");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        renumberButton.Click += RenumberButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(renumberButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void RenumberButton_Click(object sender, RoutedEventArgs e)
    {
        UseWindowsCategory = _windowsRadio?.IsChecked == true;
        Prefix = _prefixBox.Text;
        StartNumber = int.TryParse(_startBox.Text, out var s) && s >= 1 ? s : 1;
        Increment = int.TryParse(_incrementBox.Text, out var i) && i >= 1 ? i : 1;
        DialogResult = true;
        Close();
    }
}
