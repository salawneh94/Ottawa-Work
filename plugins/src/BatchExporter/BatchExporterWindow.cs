using StackPanel = System.Windows.Controls.StackPanel;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using RadioButton = System.Windows.Controls.RadioButton;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.BatchExporter;

public enum ExportFormat { Pdf, Dwg }

/// <summary>
/// Format, file-name template, output folder, and a checklist of
/// sheets/views to export — dark themed, replacing the old WinForms
/// TableLayoutPanel + CheckedListBox dialog.
/// </summary>
public class BatchExporterWindow : BimFlowWindow
{
    private readonly RadioButton _pdfRadio;
    private readonly RadioButton _dwgRadio;
    private readonly TextBox _templateBox = BimFlowUi.TextBox();
    private readonly TextBox _folderBox = BimFlowUi.TextBox();
    private readonly List<(CheckBox CheckBox, View View)> _viewChecks = new();

    public List<View> SelectedViews { get; private set; } = new();
    public ExportFormat Format { get; private set; }
    public string Template { get; private set; } = "";
    public string OutputFolder { get; private set; } = "";

    public BatchExporterWindow(List<View> candidateViews) : base("BIMFlow — Batch Exporter", minWidth: 460)
    {
        _templateBox.Text = "{SheetNumber} - {SheetName}";

        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("📤", "Batch Exporter", "Export a checklist of sheets/views to PDF or DWG in one pass."));

        var settingsStack = new StackPanel();
        settingsStack.Children.Add(BimFlowUi.FieldLabel("Format"));
        var formatRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        _pdfRadio = BimFlowUi.RadioButtonItem("PDF", "format", isChecked: true);
        _dwgRadio = BimFlowUi.RadioButtonItem("DWG", "format");
        formatRow.Children.Add(_pdfRadio);
        formatRow.Children.Add(_dwgRadio);
        settingsStack.Children.Add(formatRow);

        settingsStack.Children.Add(BimFlowUi.FieldLabel("File name template"));
        settingsStack.Children.Add(_templateBox);
        settingsStack.Children.Add(BimFlowUi.FieldLabel("Placeholders: {SheetNumber} {SheetName} {ViewName} {Index}"));

        settingsStack.Children.Add(BimFlowUi.FieldLabel("Output folder"));
        var folderRow = new StackPanel { Orientation = Orientation.Horizontal };
        _folderBox.Width = 260;
        var browseButton = BimFlowUi.SecondaryButton("Browse...");
        browseButton.Margin = new Thickness(8, 4, 0, 10);
        browseButton.Click += BrowseButton_Click;
        folderRow.Children.Add(_folderBox);
        folderRow.Children.Add(browseButton);
        settingsStack.Children.Add(folderRow);
        root.Children.Add(BimFlowUi.Card(settingsStack));

        var listStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        listStack.Children.Add(BimFlowUi.SectionHeader("Sheets/views to export"));
        var checklistStack = new StackPanel();
        foreach (var view in candidateViews)
        {
            var label = view is ViewSheet sheet ? $"{sheet.SheetNumber} - {sheet.Name}" : view.Name;
            var checkBox = BimFlowUi.CheckBoxItem(label);
            checklistStack.Children.Add(checkBox);
            _viewChecks.Add((checkBox, view));
        }
        var scroll = new ScrollViewer { MaxHeight = 260, Content = checklistStack };
        listStack.Children.Add(BimFlowUi.Card(scroll, padding: 8));
        root.Children.Add(listStack);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var exportButton = BimFlowUi.PrimaryButton("Export");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        exportButton.Click += ExportButton_Click;
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(exportButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) _folderBox.Text = dialog.SelectedPath;
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedViews = _viewChecks.Where(t => t.CheckBox.IsChecked == true).Select(t => t.View).ToList();
        Format = _dwgRadio.IsChecked == true ? ExportFormat.Dwg : ExportFormat.Pdf;
        Template = _templateBox.Text;
        OutputFolder = _folderBox.Text;
        DialogResult = true;
        Close();
    }
}
