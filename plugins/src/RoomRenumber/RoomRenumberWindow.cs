using RadioButton = System.Windows.Controls.RadioButton;
using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using OttawaWork.Shared;

namespace OttawaWork.RoomRenumber;

public enum SortDirection
{
    LeftToRightThenTopToBottom,
    TopToBottomThenLeftToRight,
}

/// <summary>Direction + prefix/start/increment picker — dark themed, replacing the old WinForms TableLayoutPanel dialog.</summary>
public class RoomRenumberWindow : OttawaWorkWindow
{
    private readonly RadioButton _topToBottomRadio;
    private readonly TextBox _prefixBox = OttawaWorkUi.TextBox();
    private readonly TextBox _startBox = OttawaWorkUi.TextBox();
    private readonly TextBox _incrementBox = OttawaWorkUi.TextBox();

    public SortDirection Direction { get; private set; } = SortDirection.LeftToRightThenTopToBottom;
    public string Prefix { get; private set; } = "";
    public int StartNumber { get; private set; } = 100;
    public int Increment { get; private set; } = 1;

    public RoomRenumberWindow(int roomCount) : base("Ottawa Tools — Room Renumber", minWidth: 380)
    {
        _startBox.Text = "100";
        _incrementBox.Text = "1";

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🔢", "Room Renumber", $"{roomCount} room(s) will be renumbered."));

        var directionStack = new StackPanel();
        directionStack.Children.Add(OttawaWorkUi.FieldLabel("Direction"));
        var leftToRightRadio = OttawaWorkUi.RadioButtonItem("Left → right, then top → bottom", "direction", isChecked: true);
        _topToBottomRadio = OttawaWorkUi.RadioButtonItem("Top → bottom, then left → right", "direction");
        directionStack.Children.Add(leftToRightRadio);
        directionStack.Children.Add(_topToBottomRadio);
        root.Children.Add(OttawaWorkUi.Card(directionStack));

        var fieldsStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        fieldsStack.Children.Add(OttawaWorkUi.FieldLabel("Prefix"));
        fieldsStack.Children.Add(_prefixBox);
        fieldsStack.Children.Add(OttawaWorkUi.FieldLabel("Start number"));
        fieldsStack.Children.Add(_startBox);
        fieldsStack.Children.Add(OttawaWorkUi.FieldLabel("Increment"));
        fieldsStack.Children.Add(_incrementBox);
        root.Children.Add(OttawaWorkUi.Card(fieldsStack));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var renumberButton = OttawaWorkUi.PrimaryButton("Renumber");
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
