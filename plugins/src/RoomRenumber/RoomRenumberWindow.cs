using RadioButton = System.Windows.Controls.RadioButton;
using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using BIMFlow.Shared;

namespace BIMFlow.RoomRenumber;

public enum SortDirection
{
    LeftToRightThenTopToBottom,
    TopToBottomThenLeftToRight,
}

/// <summary>Direction + prefix/start/increment picker — dark themed, replacing the old WinForms TableLayoutPanel dialog.</summary>
public class RoomRenumberWindow : BimFlowWindow
{
    private readonly RadioButton _topToBottomRadio;
    private readonly TextBox _prefixBox = BimFlowUi.TextBox();
    private readonly TextBox _startBox = BimFlowUi.TextBox();
    private readonly TextBox _incrementBox = BimFlowUi.TextBox();

    public SortDirection Direction { get; private set; } = SortDirection.LeftToRightThenTopToBottom;
    public string Prefix { get; private set; } = "";
    public int StartNumber { get; private set; } = 100;
    public int Increment { get; private set; } = 1;

    public RoomRenumberWindow(int roomCount) : base("BIMFlow — Room Renumber", minWidth: 380)
    {
        _startBox.Text = "100";
        _incrementBox.Text = "1";

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🔢", "Room Renumber", $"{roomCount} room(s) will be renumbered."));

        var directionStack = new StackPanel();
        directionStack.Children.Add(BimFlowUi.FieldLabel("Direction"));
        var leftToRightRadio = BimFlowUi.RadioButtonItem("Left → right, then top → bottom", "direction", isChecked: true);
        _topToBottomRadio = BimFlowUi.RadioButtonItem("Top → bottom, then left → right", "direction");
        directionStack.Children.Add(leftToRightRadio);
        directionStack.Children.Add(_topToBottomRadio);
        root.Children.Add(BimFlowUi.Card(directionStack));

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
        Direction = _topToBottomRadio.IsChecked == true ? SortDirection.TopToBottomThenLeftToRight : SortDirection.LeftToRightThenTopToBottom;
        Prefix = _prefixBox.Text;
        StartNumber = int.TryParse(_startBox.Text, out var s) && s >= 1 ? s : 100;
        Increment = int.TryParse(_incrementBox.Text, out var i) && i >= 1 ? i : 1;
        DialogResult = true;
        Close();
    }
}
