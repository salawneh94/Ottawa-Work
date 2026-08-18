using StackPanel = System.Windows.Controls.StackPanel;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

namespace BIMFlow.Shared;

/// <summary>Minimal single-line text prompt — dark themed, replacing the old bare WinForms Form.</summary>
public static class TextInputDialog
{
    public static string? Prompt(string title, string label, string defaultValue = "")
    {
        var window = new PromptWindow(title, label, defaultValue);
        return window.ShowDialog() == true ? window.Value : null;
    }

    private class PromptWindow : BimFlowWindow
    {
        private readonly TextBox _textBox = BimFlowUi.TextBox();

        public string Value { get; private set; } = "";

        public PromptWindow(string title, string label, string defaultValue) : base(title, minWidth: 320)
        {
            var root = new StackPanel();
            root.Children.Add(BimFlowUi.FieldLabel(label));
            _textBox.Text = defaultValue;
            root.Children.Add(_textBox);

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var cancelButton = BimFlowUi.SecondaryButton("Cancel");
            var okButton = BimFlowUi.PrimaryButton("OK");
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
            okButton.Click += (_, _) => { Value = _textBox.Text.Trim(); DialogResult = true; Close(); };
            buttonRow.Children.Add(cancelButton);
            buttonRow.Children.Add(okButton);
            root.Children.Add(buttonRow);

            SetContent(root);
        }
    }
}
