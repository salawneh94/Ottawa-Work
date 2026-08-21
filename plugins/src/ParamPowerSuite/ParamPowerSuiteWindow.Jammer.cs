using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using Thickness = System.Windows.Thickness;
using UIElement = System.Windows.UIElement;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.ParamPowerSuite;

public partial class ParamPowerSuiteWindow
{
    private readonly TextBox _jamParamNameBox = OttawaWorkUi.TextBox();
    private readonly TextBox _jamFilePathBox = OttawaWorkUi.TextBox();
    private readonly TextBox _jamGroupNameBox = OttawaWorkUi.TextBox();
    private readonly ComboBox _jamDataTypeBox = OttawaWorkUi.ComboBox();
    private readonly ComboBox _jamGroupTypeBox = OttawaWorkUi.ComboBox();

    private UIElement BuildJammerTab()
    {
        var stack = new StackPanel();
        stack.Children.Add(OttawaWorkUi.SectionHeader("Jammer"));
        stack.Children.Add(OttawaWorkUi.FieldLabel("Replaces a LOCAL family parameter with a SHARED one of the same name, in place — preserves every instance's current value and any formula. Operates on the distinct loadable families among the loaded elements (system-family categories like Walls/Floors have no family document and are not applicable)."));

        stack.Children.Add(OttawaWorkUi.FieldLabel("Parameter name (must already exist as a local, non-shared family parameter)"));
        stack.Children.Add(_jamParamNameBox);

        stack.Children.Add(OttawaWorkUi.FieldLabel("Shared parameter file"));
        var fileRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 10) };
        _jamFilePathBox.Width = 320;
        _jamFilePathBox.Margin = new Thickness(0, 0, 8, 0);
        var browseButton = OttawaWorkUi.SecondaryButton("Browse");
        browseButton.Click += (_, _) => BrowseJamSharedParameterFile();
        fileRow.Children.Add(_jamFilePathBox);
        fileRow.Children.Add(browseButton);
        stack.Children.Add(fileRow);

        stack.Children.Add(OttawaWorkUi.FieldLabel("Group (within the shared parameter file)"));
        _jamGroupNameBox.Text = "Ottawa Tools";
        stack.Children.Add(_jamGroupNameBox);

        stack.Children.Add(OttawaWorkUi.FieldLabel("Data type (used only if this exact parameter doesn't already exist in the file)"));
        _jamDataTypeBox.Items.AddRange(ParamPowerSuiteEngine.DataTypeOptions.Select(o => (object)o.Label));
        _jamDataTypeBox.SelectedIndex = 0;
        stack.Children.Add(_jamDataTypeBox);

        stack.Children.Add(OttawaWorkUi.FieldLabel("Parameter group / discipline"));
        _jamGroupTypeBox.Items.AddRange(ParamPowerSuiteEngine.ParameterGroupOptions.Select(o => (object)o.Label));
        _jamGroupTypeBox.SelectedIndex = 0;
        stack.Children.Add(_jamGroupTypeBox);

        var jamButton = OttawaWorkUi.DangerButton("Jam to shared parameter");
        jamButton.Margin = new Thickness(0, 16, 0, 0);
        jamButton.Click += (_, _) => RunJammer();
        stack.Children.Add(jamButton);

        return stack;
    }

    private void BrowseJamSharedParameterFile()
    {
        using var dialog = new OpenFileDialog { Title = "Choose a shared parameter file", Filter = "Shared Parameter File (*.txt)|*.txt" };
        if (dialog.ShowDialog() == WinFormsDialogResult.OK) _jamFilePathBox.Text = dialog.FileName;
    }

    private void RunJammer()
    {
        if (_loadedElements.Count == 0) { SetStatus("Jammer: no elements loaded", 0, 0, 0); return; }
        if (string.IsNullOrWhiteSpace(_jamParamNameBox.Text) || string.IsNullOrWhiteSpace(_jamFilePathBox.Text))
        {
            SetStatus("Jammer: parameter name and shared parameter file are required", 0, 0, 0);
            return;
        }

        var dataType = ParamPowerSuiteEngine.DataTypeOptions[_jamDataTypeBox.SelectedIndex].Type;
        var groupType = ParamPowerSuiteEngine.ParameterGroupOptions[_jamGroupTypeBox.SelectedIndex].Group;
        var groupName = string.IsNullOrWhiteSpace(_jamGroupNameBox.Text) ? "Ottawa Tools" : _jamGroupNameBox.Text;

        var (jammed, skipped, failed) = ParamPowerSuiteEngine.Jam(
            _doc, _loadedElements, _jamParamNameBox.Text.Trim(), _jamFilePathBox.Text.Trim(), groupName, dataType, groupType);

        SetStatus("Jammer", jammed, skipped, failed);
    }
}
