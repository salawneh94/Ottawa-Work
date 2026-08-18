using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using BIMFlow.Shared;

namespace BIMFlow.NamingConventionAudit;

public enum NamingTarget { Views, Sheets, FamilyTypes }

/// <summary>Target + regex pattern picker — dark themed, replacing the old WinForms TableLayoutPanel dialog.</summary>
public class NamingRuleWindow : BimFlowWindow
{
    private readonly ComboBox _targetBox = BimFlowUi.ComboBox();
    private readonly TextBox _patternBox = BimFlowUi.TextBox();

    public NamingTarget? Target { get; private set; }
    public string Pattern { get; private set; } = "";

    public NamingRuleWindow() : base("BIMFlow — NamingConventionAudit", minWidth: 380)
    {
        _patternBox.Text = "^[A-Z0-9_-]+$";

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🔡", "Naming Convention Audit", "Names that DON'T match the pattern will be flagged."));

        var fieldsStack = new StackPanel();
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Check"));
        _targetBox.Items.Add("Views");
        _targetBox.Items.Add("Sheets");
        _targetBox.Items.Add("Family Types");
        _targetBox.SelectedIndex = 0;
        fieldsStack.Children.Add(_targetBox);
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Must match regex"));
        fieldsStack.Children.Add(_patternBox);
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Example: ^\\d{2,3} - .+$ for \"101 - Floor Plan\""));
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
        Target = _targetBox.SelectedIndex switch
        {
            0 => NamingTarget.Views,
            1 => NamingTarget.Sheets,
            2 => NamingTarget.FamilyTypes,
            _ => null,
        };
        Pattern = _patternBox.Text;
        DialogResult = true;
        Close();
    }
}
