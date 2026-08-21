using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using Thickness = System.Windows.Thickness;
using UIElement = System.Windows.UIElement;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.ParamPowerSuite;

public partial class ParamPowerSuiteWindow
{
    private readonly TextBox _bulkCommentsBox = OttawaWorkUi.TextBox();
    private readonly TextBox _bulkMarkBox = OttawaWorkUi.TextBox();
    private readonly TextBox _bulkTypeCommentsBox = OttawaWorkUi.TextBox();
    private readonly TextBox _bulkKeynoteBox = OttawaWorkUi.TextBox();
    private readonly TextBox _bulkDescriptionBox = OttawaWorkUi.TextBox();

    private UIElement BuildBulkSetTab()
    {
        var stack = new StackPanel();
        stack.Children.Add(OttawaWorkUi.SectionHeader("Bulk Set"));
        stack.Children.Add(OttawaWorkUi.FieldLabel("Sets the same value on every loaded element for each field below — leave a field empty to skip it entirely (an empty box never clears an existing value)."));

        var fieldsStack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        AddBulkField(fieldsStack, "Comments", _bulkCommentsBox);
        AddBulkField(fieldsStack, "Mark", _bulkMarkBox);
        AddBulkField(fieldsStack, "Type Comments", _bulkTypeCommentsBox);
        AddBulkField(fieldsStack, "Keynote", _bulkKeynoteBox);
        AddBulkField(fieldsStack, "Description", _bulkDescriptionBox);
        stack.Children.Add(OttawaWorkUi.Card(fieldsStack));

        var applyButton = OttawaWorkUi.PrimaryButton("Apply bulk set");
        applyButton.Margin = new Thickness(0, 16, 0, 0);
        applyButton.Click += (_, _) => ApplyBulkSet();
        stack.Children.Add(applyButton);

        return stack;
    }

    private static void AddBulkField(StackPanel parent, string label, TextBox box)
    {
        parent.Children.Add(OttawaWorkUi.FieldLabel(label));
        parent.Children.Add(box);
    }

    private void ApplyBulkSet()
    {
        if (_loadedElements.Count == 0) { SetStatus("Bulk Set: no elements loaded", 0, 0, 0); return; }

        var values = new Dictionary<string, string>
        {
            ["Comments"] = _bulkCommentsBox.Text,
            ["Mark"] = _bulkMarkBox.Text,
            ["Type Comments"] = _bulkTypeCommentsBox.Text,
            ["Keynote"] = _bulkKeynoteBox.Text,
            ["Description"] = _bulkDescriptionBox.Text,
        };

        var result = new ParamOpResult();
        using (var transaction = new Transaction(_doc, "Param Power Suite: Bulk Set"))
        {
            transaction.Start();
            try
            {
                result = ParamPowerSuiteEngine.ApplyBulkSet(_loadedElements, values);
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.RollBack();
                SetStatus("Bulk Set: failed, rolled back", 0, 0, 0);
                return;
            }
        }

        SetStatus("Bulk Set", result);
    }
}
