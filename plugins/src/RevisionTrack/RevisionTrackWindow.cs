using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.RevisionTrack;

/// <summary>
/// Existing-or-new revision picker + issued-to/by fields + a sheet
/// checklist — dark themed, replacing the old WinForms dialog.
/// </summary>
public class RevisionTrackWindow : BimFlowWindow
{
    private readonly ComboBox _revisionBox = BimFlowUi.ComboBox();
    private readonly TextBox _newDescriptionBox = BimFlowUi.TextBox();
    private readonly TextBox _issuedToBox = BimFlowUi.TextBox();
    private readonly TextBox _issuedByBox = BimFlowUi.TextBox();
    private readonly List<Revision> _revisions;
    private readonly List<(CheckBox CheckBox, ViewSheet Sheet)> _sheetChecks = new();

    public Revision? SelectedRevision { get; private set; }
    public string NewRevisionDescription { get; private set; } = "";
    public string IssuedTo { get; private set; } = "";
    public string IssuedBy { get; private set; } = "";
    public List<ViewSheet> SelectedSheets { get; private set; } = new();

    public RevisionTrackWindow(List<Revision> revisions, List<ViewSheet> sheets) : base("BIMFlow — RevisionTrack", minWidth: 420)
    {
        _revisions = revisions;

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("📝", "Revision Track", "Add a revision to a chosen set of sheets in one pass."));

        var fieldsStack = new StackPanel();
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Existing revision (optional)"));
        _revisionBox.Items.Add("(create a new revision)");
        foreach (var r in revisions) _revisionBox.Items.Add($"{r.SequenceNumber}: {r.Description}");
        _revisionBox.SelectedIndex = 0;
        fieldsStack.Children.Add(_revisionBox);

        fieldsStack.Children.Add(BimFlowUi.FieldLabel("New revision description"));
        fieldsStack.Children.Add(_newDescriptionBox);
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Issued to"));
        fieldsStack.Children.Add(_issuedToBox);
        fieldsStack.Children.Add(BimFlowUi.FieldLabel("Issued by"));
        fieldsStack.Children.Add(_issuedByBox);
        root.Children.Add(BimFlowUi.Card(fieldsStack));

        var sheetsStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        sheetsStack.Children.Add(BimFlowUi.SectionHeader("Add this revision to these sheets"));
        var checklistStack = new StackPanel();
        foreach (var sheet in sheets)
        {
            var checkBox = BimFlowUi.CheckBoxItem($"{sheet.SheetNumber} - {sheet.Name}");
            checklistStack.Children.Add(checkBox);
            _sheetChecks.Add((checkBox, sheet));
        }
        var scroll = new ScrollViewer { MaxHeight = 220, Content = checklistStack };
        sheetsStack.Children.Add(BimFlowUi.Card(scroll, padding: 8));
        root.Children.Add(sheetsStack);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var applyButton = BimFlowUi.PrimaryButton("Apply");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        applyButton.Click += ApplyButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(applyButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedRevision = _revisionBox.SelectedIndex > 0 ? _revisions[_revisionBox.SelectedIndex - 1] : null;
        NewRevisionDescription = _newDescriptionBox.Text;
        IssuedTo = _issuedToBox.Text;
        IssuedBy = _issuedByBox.Text;
        SelectedSheets = _sheetChecks.Where(t => t.CheckBox.IsChecked == true).Select(t => t.Sheet).ToList();
        DialogResult = true;
        Close();
    }
}
