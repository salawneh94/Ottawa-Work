using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
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
    private readonly TextBox _createParamFilePathBox = OttawaWorkUi.TextBox();
    private readonly TextBox _createParamGroupNameBox = OttawaWorkUi.TextBox();
    private readonly TextBox _createParamNameBox = OttawaWorkUi.TextBox();
    private readonly ComboBox _createParamDataTypeBox = OttawaWorkUi.ComboBox();
    private readonly ComboBox _createParamGroupTypeBox = OttawaWorkUi.ComboBox();
    private readonly Button _createParamInstanceButton = OttawaWorkUi.SecondaryButton("Instance");
    private readonly Button _createParamTypeButton = OttawaWorkUi.SecondaryButton("Type");
    private bool _createParamIsInstance = true;
    private readonly Dictionary<BuiltInCategory, CheckBox> _createParamCategoryChecks = new();

    private UIElement BuildCreateParamTab()
    {
        var stack = new StackPanel();
        stack.Children.Add(OttawaWorkUi.SectionHeader("Create Param"));
        stack.Children.Add(OttawaWorkUi.FieldLabel("Creates a new parameter (or reuses one already in the file) and binds it into this project — the standard way to add a project parameter through the API, since there's no direct 'create a pure project parameter' call."));

        stack.Children.Add(OttawaWorkUi.FieldLabel("Shared parameter file (picks an existing one, or starts a new one at the path you type)"));
        var fileRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 10) };
        _createParamFilePathBox.Width = 320;
        _createParamFilePathBox.Margin = new Thickness(0, 0, 8, 0);
        var browseButton = OttawaWorkUi.SecondaryButton("Browse");
        browseButton.Click += (_, _) => BrowseCreateParamSharedParameterFile();
        fileRow.Children.Add(_createParamFilePathBox);
        fileRow.Children.Add(browseButton);
        stack.Children.Add(fileRow);

        stack.Children.Add(OttawaWorkUi.FieldLabel("Group (within the shared parameter file)"));
        _createParamGroupNameBox.Text = "Ottawa Tools";
        stack.Children.Add(_createParamGroupNameBox);

        stack.Children.Add(OttawaWorkUi.FieldLabel("Parameter name"));
        stack.Children.Add(_createParamNameBox);

        stack.Children.Add(OttawaWorkUi.FieldLabel("Data type"));
        _createParamDataTypeBox.Items.AddRange(ParamPowerSuiteEngine.DataTypeOptions.Select(o => (object)o.Label));
        _createParamDataTypeBox.SelectedIndex = 0;
        stack.Children.Add(_createParamDataTypeBox);

        stack.Children.Add(OttawaWorkUi.FieldLabel("Discipline / group"));
        _createParamGroupTypeBox.Items.AddRange(ParamPowerSuiteEngine.ParameterGroupOptions.Select(o => (object)o.Label));
        _createParamGroupTypeBox.SelectedIndex = 0;
        stack.Children.Add(_createParamGroupTypeBox);

        var scopeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 12) };
        _createParamInstanceButton.Margin = new Thickness(0, 0, 8, 0);
        _createParamInstanceButton.Click += (_, _) => SetCreateParamScope(true);
        _createParamTypeButton.Click += (_, _) => SetCreateParamScope(false);
        scopeRow.Children.Add(_createParamInstanceButton);
        scopeRow.Children.Add(_createParamTypeButton);
        stack.Children.Add(scopeRow);
        SetCreateParamScope(true);

        stack.Children.Add(OttawaWorkUi.SectionHeader("Bind to categories"));
        var categoryScroll = new ScrollViewer { MaxHeight = 160 };
        var categoryList = new StackPanel();
        foreach (var (label, builtIn) in CommonCategories.Roster)
        {
            var check = OttawaWorkUi.CheckBoxItem(label);
            check.FontSize = 12;
            _createParamCategoryChecks[builtIn] = check;
            categoryList.Children.Add(check);
        }
        categoryScroll.Content = categoryList;
        stack.Children.Add(OttawaWorkUi.Card(categoryScroll, padding: 8));

        var createButton = OttawaWorkUi.SuccessButton("Create + Bind");
        createButton.Margin = new Thickness(0, 16, 0, 0);
        createButton.Click += (_, _) => RunCreateParam();
        stack.Children.Add(createButton);

        return stack;
    }

    private void SetCreateParamScope(bool isInstance)
    {
        _createParamIsInstance = isInstance;
        OttawaWorkUi.SetToggleActive(_createParamInstanceButton, isInstance);
        OttawaWorkUi.SetToggleActive(_createParamTypeButton, !isInstance);
    }

    private void BrowseCreateParamSharedParameterFile()
    {
        using var dialog = new OpenFileDialog { Title = "Choose a shared parameter file", Filter = "Shared Parameter File (*.txt)|*.txt" };
        if (dialog.ShowDialog() == WinFormsDialogResult.OK) _createParamFilePathBox.Text = dialog.FileName;
    }

    private void RunCreateParam()
    {
        if (string.IsNullOrWhiteSpace(_createParamFilePathBox.Text) || string.IsNullOrWhiteSpace(_createParamNameBox.Text))
        {
            SetStatus("Create Param: file path and parameter name are required", 0, 0, 0);
            return;
        }

        var categories = _createParamCategoryChecks.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
        if (categories.Count == 0) { SetStatus("Create Param: pick at least one category", 0, 0, 0); return; }

        var filePath = _createParamFilePathBox.Text.Trim();
        var groupName = string.IsNullOrWhiteSpace(_createParamGroupNameBox.Text) ? "Ottawa Tools" : _createParamGroupNameBox.Text;
        var dataType = ParamPowerSuiteEngine.DataTypeOptions[_createParamDataTypeBox.SelectedIndex].Type;
        var groupType = ParamPowerSuiteEngine.ParameterGroupOptions[_createParamGroupTypeBox.SelectedIndex].Group;

        ParamPowerSuiteEngine.EnsureSharedParameterFileExists(filePath);

        var created = false;
        using (var transaction = new Transaction(_doc, "Param Power Suite: Create + Bind Parameter"))
        {
            transaction.Start();
            try
            {
                created = ParamPowerSuiteEngine.CreateAndBindParameter(
                    _doc, filePath, groupName, _createParamNameBox.Text.Trim(), dataType, groupType, _createParamIsInstance, categories);
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.RollBack();
                SetStatus("Create Param: failed, rolled back", 0, 0, 0);
                return;
            }
        }

        SetStatus(created ? "Create Param: bound" : "Create Param: failed (no bindable category resolved)", created ? 1 : 0, 0, created ? 0 : 1);
    }
}
