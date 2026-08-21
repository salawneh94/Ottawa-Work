using ComboBox = System.Windows.Controls.ComboBox;
using RadioButton = System.Windows.Controls.RadioButton;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using Thickness = System.Windows.Thickness;
using UIElement = System.Windows.UIElement;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.ParamPowerSuite;

public partial class ParamPowerSuiteWindow
{
    private const string CaseModeGroup = "ParamPowerSuiteCaseMode";

    private readonly ComboBox _ctParamBox = BimFlowUi.ComboBox();
    private readonly RadioButton _ctUpperOption = BimFlowUi.RadioButtonItem("UPPERCASE", CaseModeGroup, isChecked: true);
    private readonly RadioButton _ctLowerOption = BimFlowUi.RadioButtonItem("lowercase", CaseModeGroup);
    private readonly RadioButton _ctTitleOption = BimFlowUi.RadioButtonItem("Title Case", CaseModeGroup);
    private readonly StackPanel _ctPreviewPanel = new();
    private List<ParamChangePreview> _ctPendingChanges = new();

    private UIElement BuildCaseTransformTab()
    {
        _parameterDependentComboBoxes.Add(_ctParamBox);

        var stack = new StackPanel();
        stack.Children.Add(BimFlowUi.SectionHeader("Case Transform"));
        stack.Children.Add(BimFlowUi.FieldLabel("Target parameter (text-storage only)"));
        stack.Children.Add(_ctParamBox);

        var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 12) };
        modeRow.Children.Add(_ctUpperOption);
        modeRow.Children.Add(_ctLowerOption);
        modeRow.Children.Add(_ctTitleOption);
        stack.Children.Add(modeRow);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var previewButton = BimFlowUi.SecondaryButton("Preview");
        previewButton.Margin = new Thickness(0, 0, 8, 0);
        previewButton.Click += (_, _) => PreviewCaseTransform();
        var applyButton = BimFlowUi.PrimaryButton("Apply");
        applyButton.Click += (_, _) => ApplyCaseTransform();
        buttonRow.Children.Add(previewButton);
        buttonRow.Children.Add(applyButton);
        stack.Children.Add(buttonRow);

        var scroll = new ScrollViewer { MaxHeight = 260, Content = _ctPreviewPanel };
        stack.Children.Add(BimFlowUi.Card(scroll, padding: 10));

        return stack;
    }

    private CaseMode SelectedCaseMode() => _ctUpperOption.IsChecked == true ? CaseMode.Upper
        : _ctLowerOption.IsChecked == true ? CaseMode.Lower
        : CaseMode.Title;

    private void PreviewCaseTransform()
    {
        _ctPendingChanges = new List<ParamChangePreview>();
        _ctPreviewPanel.Children.Clear();

        if (_loadedElements.Count == 0 || _ctParamBox.SelectedItem is not string paramName) return;

        _ctPendingChanges = ParamPowerSuiteEngine.PreviewCaseTransform(_loadedElements, paramName, SelectedCaseMode());
        _ctPreviewPanel.Children.Add(BuildPreviewList(_ctPendingChanges));
    }

    private void ApplyCaseTransform()
    {
        if (_ctParamBox.SelectedItem is not string paramName) { SetStatus("Case Transform: pick a parameter first", 0, 0, 0); return; }

        PreviewCaseTransform();
        if (_ctPendingChanges.Count == 0) { SetStatus("Case Transform: nothing to change", 0, 0, 0); return; }

        var result = new ParamOpResult();
        using (var transaction = new Transaction(_doc, "Param Power Suite: Case Transform"))
        {
            transaction.Start();
            try
            {
                result = ParamPowerSuiteEngine.ApplyChanges(_doc, _ctPendingChanges, paramName);
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.RollBack();
                SetStatus("Case Transform: failed, rolled back", 0, 0, 0);
                return;
            }
        }

        SetStatus("Case Transform", result);
    }
}
