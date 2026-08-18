using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using BIMFlow.Shared;

namespace BIMFlow.StandardsChecker;

public enum CheckTarget { ViewNames, SheetNumbers, SheetNames }

/// <summary>Target + approved-pattern picker — dark themed, replacing the old WinForms TableLayoutPanel dialog.</summary>
public class StandardsCheckerWindow : BimFlowWindow
{
    private readonly ComboBox _targetBox = BimFlowUi.ComboBox();
    private readonly TextBox _patternBox = BimFlowUi.TextBox();

    public CheckTarget Target { get; private set; }
    public string Pattern { get; private set; } = "";

    public StandardsCheckerWindow() : base("BIMFlow — StandardsChecker", minWidth: 380)
    {
        _patternBox.Text = "^[A-Z0-9_ -]+$";

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("📐", "Standards Checker", "Anything that doesn't match this pattern is flagged."));

        var fieldsStack = new StackPanel();
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Check"));
        _targetBox.Items.Add("View names");
        _targetBox.Items.Add("Sheet numbers");
        _targetBox.Items.Add("Sheet names");
        _targetBox.SelectedIndex = 0;
        fieldsStack.Children.Add(_targetBox);
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Approved pattern (regex)"));
        fieldsStack.Children.Add(_patternBox);
        root.Children.Add(BimFlowUi.Card(fieldsStack));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var runButton = BimFlowUi.PrimaryButton("Run audit");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        runButton.Click += RunButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(runButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        Target = (CheckTarget)_targetBox.SelectedIndex;
        Pattern = _patternBox.Text;
        DialogResult = true;
        Close();
    }
}
