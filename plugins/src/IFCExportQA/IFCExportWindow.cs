using ComboBox = System.Windows.Controls.ComboBox;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

using Autodesk.Revit.DB;
using BIMFlow.Shared;

namespace BIMFlow.IFCExportQA;

/// <summary>IFC version picker + a read-only pre-flight category summary — dark themed, replacing the old WinForms ListView dialog.</summary>
public class IFCExportWindow : BimFlowWindow
{
    private readonly ComboBox _versionBox = BimFlowUi.ComboBox();

    public IFCVersion SelectedVersion { get; private set; }

    public IFCExportWindow(List<(string Category, int Count)> categorySummary) : base("BIMFlow — IFCExportQA", minWidth: 420)
    {
        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🏗️", "IFC Export QA", $"Pre-flight: {categorySummary.Sum(c => c.Count)} element(s) across {categorySummary.Count} categories will be exported."));

        var versionStack = new StackPanel();
        versionStack.Children.Add(BimFlowUi.FieldLabel("IFC version"));
        _versionBox.Items.Add("IFC2x3 Coordination View 2.0");
        _versionBox.Items.Add("IFC4");
        _versionBox.SelectedIndex = 0;
        versionStack.Children.Add(_versionBox);
        root.Children.Add(BimFlowUi.Card(versionStack));

        var summaryStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        summaryStack.Children.Add(BimFlowUi.SectionHeader("Categories"));
        var rowsStack = new StackPanel();
        foreach (var (category, count) in categorySummary.OrderByDescending(c => c.Count))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            row.Children.Add(new TextBlock { Text = category, FontSize = 12, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextPrimary), Width = 260 });
            row.Children.Add(BimFlowUi.Badge(count.ToString()));
            rowsStack.Children.Add(row);
        }
        var scroll = new ScrollViewer { MaxHeight = 280, Content = rowsStack };
        summaryStack.Children.Add(BimFlowUi.Card(scroll, padding: 8));
        root.Children.Add(summaryStack);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var exportButton = BimFlowUi.PrimaryButton("Export...");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        exportButton.Click += (_, _) =>
        {
            SelectedVersion = _versionBox.SelectedIndex == 1 ? IFCVersion.IFC4 : IFCVersion.IFC2x3CV2;
            DialogResult = true;
            Close();
        };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(exportButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }
}
