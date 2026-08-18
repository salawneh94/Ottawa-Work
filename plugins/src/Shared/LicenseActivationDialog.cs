using StackPanel = System.Windows.Controls.StackPanel;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using TextWrapping = System.Windows.TextWrapping;

namespace BIMFlow.Shared;

/// <summary>Dark-themed prompt for the one-time license key entry, replacing the old bare WinForms Form.</summary>
public static class LicenseActivationDialog
{
    public static string? PromptForKey()
    {
        var window = new PromptWindow();
        return window.ShowDialog() == true ? window.Key : null;
    }

    private class PromptWindow : BimFlowWindow
    {
        private readonly TextBox _textBox = BimFlowUi.TextBox();

        public string Key { get; private set; } = "";

        public PromptWindow() : base("Activate BIMFlow Plugin", minWidth: 380)
        {
            var root = new StackPanel();
            root.Children.Add(BimFlowUi.TitleBar("🔑", "Activate BIMFlow", "Enter your license key (find it in your account's Licenses page)."));

            root.Children.Add(_textBox);

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var cancelButton = BimFlowUi.SecondaryButton("Cancel");
            var activateButton = BimFlowUi.PrimaryButton("Activate");
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
            activateButton.Click += (_, _) => { Key = _textBox.Text.Trim(); DialogResult = true; Close(); };
            buttonRow.Children.Add(cancelButton);
            buttonRow.Children.Add(activateButton);
            root.Children.Add(buttonRow);

            SetContent(root);
        }
    }
}
