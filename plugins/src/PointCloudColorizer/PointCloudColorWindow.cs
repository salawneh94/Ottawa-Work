using ColorDialogWinForms = System.Windows.Forms.ColorDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;
using RadioButton = System.Windows.Controls.RadioButton;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Thickness = System.Windows.Thickness;
using CornerRadius = System.Windows.CornerRadius;
using WpfColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

using RevitColor = Autodesk.Revit.DB.Color;
using OttawaWork.Shared;

namespace OttawaWork.PointCloudColorizer;

public enum PointCloudColorAction { ApplyColor, ResetToDefault }
public enum PointCloudScope { AllInView, SelectedOnly }

/// <summary>
/// Preset palette + custom color picker for tinting point cloud instances,
/// with a scope choice (every point cloud in the view, or just the
/// selected ones) — Apply Color / Reset to Default.
/// </summary>
public class PointCloudColorWindow : OttawaWorkWindow
{
    private static readonly (string Name, RevitColor Color)[] Presets =
    {
        ("Black", new RevitColor(26, 26, 26)),
        ("Gray", new RevitColor(128, 128, 128)),
        ("Red", new RevitColor(220, 40, 40)),
        ("Blue", new RevitColor(40, 100, 220)),
        ("Green", new RevitColor(40, 167, 69)),
        ("Purple", new RevitColor(139, 40, 220)),
        ("Yellow", new RevitColor(230, 190, 30)),
    };

    private readonly TextBlock _selectedLabel = new() { FontSize = 13, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary) };
    private readonly Border _selectedSwatch = new() { Width = 32, Height = 32, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 10, 0) };
    private readonly RadioButton _allBox;
    private readonly RadioButton _selectedOnlyBox;

    public RevitColor SelectedColor { get; private set; } = Presets[2].Color;
    public PointCloudScope Scope { get; private set; } = PointCloudScope.AllInView;
    public PointCloudColorAction ChosenAction { get; private set; }

    public PointCloudColorWindow(bool hasSelection) : base("Ottawa Tools — Point Cloud Color", minWidth: 380)
    {
        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🌈", "Point Cloud Color", "Override point cloud display color in the active view.", Close));

        var paletteStack = new StackPanel();
        paletteStack.Children.Add(OttawaWorkUi.SectionHeader("Colour palette"));
        var swatchRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        foreach (var (name, color) in Presets)
        {
            var swatch = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(WpfColor.FromRgb(color.Red, color.Green, color.Blue)),
                BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.BorderColor),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            swatch.MouseLeftButtonUp += (_, _) => SetSelectedColor(name, color);
            swatchRow.Children.Add(swatch);
        }
        paletteStack.Children.Add(swatchRow);

        var customButton = OttawaWorkUi.SecondaryButton("Pick custom color...");
        customButton.HorizontalAlignment = HorizontalAlignment.Left;
        customButton.Click += (_, _) =>
        {
            using var dialog = new ColorDialogWinForms { FullOpen = true };
            if (dialog.ShowDialog() == WinFormsDialogResult.OK)
                SetSelectedColor("Custom", new RevitColor(dialog.Color.R, dialog.Color.G, dialog.Color.B));
        };
        paletteStack.Children.Add(customButton);
        root.Children.Add(OttawaWorkUi.Card(paletteStack));

        var selectedStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        selectedStack.Children.Add(_selectedSwatch);
        selectedStack.Children.Add(_selectedLabel);
        root.Children.Add(selectedStack);

        var scopeStack = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        scopeStack.Children.Add(OttawaWorkUi.SectionHeader("Scope"));
        _allBox = OttawaWorkUi.RadioButtonItem("All point clouds in view", "pc-scope", isChecked: true);
        _selectedOnlyBox = OttawaWorkUi.RadioButtonItem("Selected point clouds only", "pc-scope");
        _selectedOnlyBox.IsEnabled = hasSelection;
        scopeStack.Children.Add(_allBox);
        scopeStack.Children.Add(_selectedOnlyBox);
        root.Children.Add(scopeStack);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var resetButton = OttawaWorkUi.SecondaryButton("Reset to default");
        var applyButton = OttawaWorkUi.PrimaryButton("Apply color");
        cancelButton.Margin = new Thickness(0, 0, 8, 0);
        resetButton.Margin = new Thickness(0, 0, 8, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        resetButton.Click += (_, _) => Finish(PointCloudColorAction.ResetToDefault);
        applyButton.Click += (_, _) => Finish(PointCloudColorAction.ApplyColor);
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(resetButton);
        buttonRow.Children.Add(applyButton);
        root.Children.Add(buttonRow);

        SetContent(root);

        SetSelectedColor(Presets[2].Name, Presets[2].Color);
    }

    private void SetSelectedColor(string name, RevitColor color)
    {
        SelectedColor = color;
        _selectedSwatch.Background = new SolidColorBrush(WpfColor.FromRgb(color.Red, color.Green, color.Blue));
        _selectedLabel.Text = $"{name}   #{color.Red:X2}{color.Green:X2}{color.Blue:X2}   R {color.Red}  G {color.Green}  B {color.Blue}";
    }

    private void Finish(PointCloudColorAction action)
    {
        ChosenAction = action;
        Scope = _selectedOnlyBox.IsChecked == true ? PointCloudScope.SelectedOnly : PointCloudScope.AllInView;
        DialogResult = true;
        Close();
    }
}
