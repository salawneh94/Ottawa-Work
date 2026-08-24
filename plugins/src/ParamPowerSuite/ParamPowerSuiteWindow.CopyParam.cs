using ComboBox = System.Windows.Controls.ComboBox;
using Button = System.Windows.Controls.Button;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using Thickness = System.Windows.Thickness;
using UIElement = System.Windows.UIElement;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.ParamPowerSuite;

public partial class ParamPowerSuiteWindow
{
    private readonly ComboBox _copySourceBox = OttawaWorkUi.ComboBox();
    private readonly ComboBox _copyTargetBox = OttawaWorkUi.ComboBox();
    private readonly Button _copyOverwriteButton = OttawaWorkUi.SecondaryButton("Overwrite");
    private readonly Button _copyAppendButton = OttawaWorkUi.SecondaryButton("Append");
    private CopyMode _copyMode = CopyMode.Overwrite;
    private readonly StackPanel _copyPreviewPanel = new();
    private List<ParamChangePreview> _copyPendingChanges = new();

    private UIElement BuildCopyTab()
    {
        _parameterDependentComboBoxes.Add(_copySourceBox);
        _parameterDependentComboBoxes.Add(_copyTargetBox);

        var stack = new StackPanel();
        stack.Children.Add(OttawaWorkUi.SectionHeader("Copy A → B"));

        stack.Children.Add(OttawaWorkUi.FieldLabel("Source parameter (A)"));
        stack.Children.Add(_copySourceBox);
        stack.Children.Add(OttawaWorkUi.FieldLabel("Target parameter (B)"));
        stack.Children.Add(_copyTargetBox);

        var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        _copyOverwriteButton.Margin = new Thickness(0, 0, 8, 0);
        _copyOverwriteButton.Click += (_, _) => SetCopyMode(CopyMode.Overwrite);
        _copyAppendButton.Click += (_, _) => SetCopyMode(CopyMode.Append);
        modeRow.Children.Add(_copyOverwriteButton);
        modeRow.Children.Add(_copyAppendButton);
        stack.Children.Add(modeRow);
        SetCopyMode(CopyMode.Overwrite);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var previewButton = OttawaWorkUi.SecondaryButton("Preview");
        previewButton.Margin = new Thickness(0, 0, 8, 0);
        previewButton.Click += (_, _) => PreviewCopy();
        var applyButton = OttawaWorkUi.PrimaryButton("Apply");
        applyButton.Click += (_, _) => ApplyCopy();
        buttonRow.Children.Add(previewButton);
        buttonRow.Children.Add(applyButton);
        stack.Children.Add(buttonRow);

        var scroll = new ScrollViewer { MaxHeight = 260, Content = _copyPreviewPanel };
        stack.Children.Add(OttawaWorkUi.Card(scroll, padding: 10));

        return stack;
    }

    private void SetCopyMode(CopyMode mode)
    {
        _copyMode = mode;
        OttawaWorkUi.SetToggleActive(_copyOverwriteButton, mode == CopyMode.Overwrite);
        OttawaWorkUi.SetToggleActive(_copyAppendButton, mode == CopyMode.Append);
    }

    private void PreviewCopy()
    {
        _copyPendingChanges = new List<ParamChangePreview>();
        _copyPreviewPanel.Children.Clear();

        if (_loadedElements.Count == 0 || _copySourceBox.SelectedItem is not string source || _copyTargetBox.SelectedItem is not string target) return;

        _copyPendingChanges = ParamPowerSuiteEngine.PreviewCopy(_loadedElements, source, target, _copyMode);
        _copyPreviewPanel.Children.Add(BuildPreviewList(_copyPendingChanges));
    }

    private void ApplyCopy()
    {
        if (_copyTargetBox.SelectedItem is not string target) { SetStatus("Copy A → B: pick both parameters first", 0, 0, 0); return; }

        PreviewCopy();
        if (_copyPendingChanges.Count == 0) { SetStatus("Copy A → B: nothing to change", 0, 0, 0); return; }

        var result = new ParamOpResult();
        var failures = new List<ParamPowerSuiteEngine.ApplyFailure>();
        using (var transaction = new Transaction(_doc, "Param Power Suite: Copy A to B"))
        {
            transaction.Start();
            try
            {
                (result, failures) = ParamPowerSuiteEngine.ApplyChanges(_doc, _copyPendingChanges, target);
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.RollBack();
                SetStatus("Copy A → B: failed, rolled back", 0, 0, 0);
                return;
            }
        }

        SetStatus("Copy A → B", result);
        ShowApplyFailures("Copy A → B", failures);
    }
}
