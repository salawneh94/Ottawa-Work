using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
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
    private readonly ComboBox _frParamBox = OttawaWorkUi.ComboBox();
    private readonly TextBox _frFindBox = OttawaWorkUi.TextBox();
    private readonly TextBox _frReplaceBox = OttawaWorkUi.TextBox();
    private readonly CheckBox _frRegexCheck = OttawaWorkUi.CheckBoxItem("Regex (.NET)");
    private readonly CheckBox _frCaseCheck = OttawaWorkUi.CheckBoxItem("Case-sensitive");
    private readonly StackPanel _frPreviewPanel = new();
    private List<ParamChangePreview> _frPendingChanges = new();

    private UIElement BuildFindReplaceTab()
    {
        _parameterDependentComboBoxes.Add(_frParamBox);

        var stack = new StackPanel();
        stack.Children.Add(OttawaWorkUi.SectionHeader("Find / Replace"));
        stack.Children.Add(OttawaWorkUi.FieldLabel("Target parameter (text-storage only)"));
        stack.Children.Add(_frParamBox);

        stack.Children.Add(OttawaWorkUi.FieldLabel("Find"));
        stack.Children.Add(_frFindBox);
        stack.Children.Add(OttawaWorkUi.FieldLabel("Replace with"));
        stack.Children.Add(_frReplaceBox);

        var toggleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        toggleRow.Children.Add(_frRegexCheck);
        _frRegexCheck.Margin = new Thickness(0, 0, 20, 0);
        toggleRow.Children.Add(_frCaseCheck);
        stack.Children.Add(toggleRow);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var previewButton = OttawaWorkUi.SecondaryButton("Preview");
        previewButton.Margin = new Thickness(0, 0, 8, 0);
        previewButton.Click += (_, _) => PreviewFindReplace();
        var applyButton = OttawaWorkUi.PrimaryButton("Apply");
        applyButton.Click += (_, _) => ApplyFindReplace();
        buttonRow.Children.Add(previewButton);
        buttonRow.Children.Add(applyButton);
        stack.Children.Add(buttonRow);

        var scroll = new ScrollViewer { MaxHeight = 260, Content = _frPreviewPanel };
        stack.Children.Add(OttawaWorkUi.Card(scroll, padding: 10));

        return stack;
    }

    private void PreviewFindReplace()
    {
        _frPendingChanges = new List<ParamChangePreview>();
        _frPreviewPanel.Children.Clear();

        if (_loadedElements.Count == 0 || _frParamBox.SelectedItem is not string paramName) return;

        _frPendingChanges = ParamPowerSuiteEngine.PreviewFindReplace(
            _loadedElements, paramName, _frFindBox.Text, _frReplaceBox.Text,
            _frRegexCheck.IsChecked == true, _frCaseCheck.IsChecked == true);

        _frPreviewPanel.Children.Add(BuildPreviewList(_frPendingChanges));
    }

    private void ApplyFindReplace()
    {
        if (_frParamBox.SelectedItem is not string paramName) { SetStatus("Find/Replace: pick a parameter first", 0, 0, 0); return; }

        PreviewFindReplace();
        if (_frPendingChanges.Count == 0) { SetStatus("Find/Replace: nothing to change", 0, 0, 0); return; }

        var result = new ParamOpResult();
        using (var transaction = new Transaction(_doc, "Param Power Suite: Find/Replace"))
        {
            transaction.Start();
            try
            {
                result = ParamPowerSuiteEngine.ApplyChanges(_doc, _frPendingChanges, paramName);
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.RollBack();
                SetStatus("Find/Replace: failed, rolled back", 0, 0, 0);
                return;
            }
        }

        SetStatus("Find/Replace", result);
    }
}
