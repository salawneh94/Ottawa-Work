using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using Thickness = System.Windows.Thickness;
using TextWrapping = System.Windows.TextWrapping;
using UIElement = System.Windows.UIElement;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.ParamPowerSuite;

public partial class ParamPowerSuiteWindow
{
    private readonly TextBox _combineTemplateBox = BimFlowUi.TextBox();
    private readonly ComboBox _combineTargetBox = BimFlowUi.ComboBox();
    private readonly TextBlock _combineLiveResultText = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = BimFlowUi.BrushOf(BimFlowUi.Success) };
    private readonly StackPanel _combinePreviewPanel = new();
    private List<ParamChangePreview> _combinePendingChanges = new();

    private UIElement BuildCombineTab()
    {
        _parameterDependentComboBoxes.Add(_combineTargetBox);

        var stack = new StackPanel();
        stack.Children.Add(BimFlowUi.SectionHeader("Combine (Tokens)"));
        stack.Children.Add(BimFlowUi.FieldLabel("Template — use {Type Name}, {Family Name}, {Level}, or any parameter name in braces, e.g. \"{Type Name} - {Mark}\""));
        _combineTemplateBox.TextChanged += (_, _) => RefreshCombineLivePreview();
        stack.Children.Add(_combineTemplateBox);

        stack.Children.Add(BimFlowUi.FieldLabel("Target parameter"));
        _combineTargetBox.SelectionChanged += (_, _) => RefreshCombineLivePreview();
        stack.Children.Add(_combineTargetBox);

        stack.Children.Add(BimFlowUi.FieldLabel("Live result (first loaded element)"));
        stack.Children.Add(BimFlowUi.Card(_combineLiveResultText));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 12) };
        var previewButton = BimFlowUi.SecondaryButton("Preview all");
        previewButton.Margin = new Thickness(0, 0, 8, 0);
        previewButton.Click += (_, _) => PreviewCombine();
        var applyButton = BimFlowUi.PrimaryButton("Apply");
        applyButton.Click += (_, _) => ApplyCombine();
        buttonRow.Children.Add(previewButton);
        buttonRow.Children.Add(applyButton);
        stack.Children.Add(buttonRow);

        var scroll = new ScrollViewer { MaxHeight = 260, Content = _combinePreviewPanel };
        stack.Children.Add(BimFlowUi.Card(scroll, padding: 10));

        return stack;
    }

    private void RefreshCombineLivePreview()
    {
        if (_loadedElements.Count == 0)
        {
            _combineLiveResultText.Text = "(load elements to see a live example)";
            return;
        }

        _combineLiveResultText.Text = ParamPowerSuiteEngine.ResolveCombineTemplate(_combineTemplateBox.Text, _loadedElements[0]);
    }

    private void PreviewCombine()
    {
        _combinePendingChanges = new List<ParamChangePreview>();
        _combinePreviewPanel.Children.Clear();

        if (_loadedElements.Count == 0 || _combineTargetBox.SelectedItem is not string targetParam) return;

        _combinePendingChanges = ParamPowerSuiteEngine.PreviewCombine(_loadedElements, _combineTemplateBox.Text, targetParam);
        _combinePreviewPanel.Children.Add(BuildPreviewList(_combinePendingChanges));
    }

    private void ApplyCombine()
    {
        if (_combineTargetBox.SelectedItem is not string targetParam) { SetStatus("Combine: pick a target parameter first", 0, 0, 0); return; }

        PreviewCombine();
        if (_combinePendingChanges.Count == 0) { SetStatus("Combine: nothing to change", 0, 0, 0); return; }

        var result = new ParamOpResult();
        using (var transaction = new Transaction(_doc, "Param Power Suite: Combine"))
        {
            transaction.Start();
            try
            {
                result = ParamPowerSuiteEngine.ApplyChanges(_doc, _combinePendingChanges, targetParam);
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.RollBack();
                SetStatus("Combine: failed, rolled back", 0, 0, 0);
                return;
            }
        }

        SetStatus("Combine", result);
    }
}
