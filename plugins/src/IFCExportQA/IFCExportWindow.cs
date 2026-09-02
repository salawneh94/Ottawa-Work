using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using Visibility = System.Windows.Visibility;

using Autodesk.Revit.DB;
using OttawaWork.Shared;

namespace OttawaWork.IFCExportQA;

/// <summary>
/// IFC version picker + a read-only pre-flight category summary, plus a
/// collapsible "Advanced Settings" section (coordinate base, property-set/
/// data toggles, geometry scope) — dark themed, replacing the old WinForms
/// ListView dialog. A real System.Windows.Controls.Expander was avoided here:
/// every other control in this codebase needed a full custom ControlTemplate
/// to escape the default OS theme chrome (see OttawaWorkUi.ComboBox's doc
/// comment — a stock template left selected-item text invisible in Revit),
/// so a hand-rolled toggle button + Visibility-swapped panel keeps the same
/// zero-OS-theme-dependency guarantee without writing a from-scratch
/// Expander template.
/// </summary>
public class IFCExportWindow : OttawaWorkWindow
{
    private readonly ComboBox _versionBox = OttawaWorkUi.ComboBox();
    private readonly ComboBox _coordinateBaseBox = OttawaWorkUi.ComboBox();
    private readonly CheckBox _commonPsetsCheck = OttawaWorkUi.CheckBoxItem("Export IFC Common Property Sets (Pset_*)", isChecked: true);
    private readonly CheckBox _baseQuantitiesCheck = OttawaWorkUi.CheckBoxItem("Export Base Quantities (Qto_*)", isChecked: true);
    private readonly CheckBox _revitPsetsCheck = OttawaWorkUi.CheckBoxItem("Export Revit Property Sets");
    private readonly CheckBox _storeGuidCheck = OttawaWorkUi.CheckBoxItem("Store IFC GUID in Model Parameters after Export");
    private readonly CheckBox _visibleOnlyCheck = OttawaWorkUi.CheckBoxItem("Export only elements visible in active view");
    private readonly CheckBox _splitWallsColumnsCheck = OttawaWorkUi.CheckBoxItem("Split multi-level walls and columns by building stories");
    private readonly StackPanel _advancedPanel;
    private readonly System.Windows.Controls.Button _advancedToggle = OttawaWorkUi.SecondaryButton("▸ Advanced Settings");

    public IFCVersion SelectedVersion { get; private set; }

    /// <summary>One of "Internal", "Project", "Site", "Shared" — the exact <c>AddOption("SitePlacement", ...)</c> value names Revit's own IFC exporter (Autodesk/revit-ifc) expects, not a friendly label.</summary>
    public string CoordinateBase { get; private set; } = "Internal";
    public bool ExportIFCCommonPropertySets { get; private set; }
    public bool ExportBaseQuantities { get; private set; }
    public bool ExportRevitPropertySets { get; private set; }
    public bool StoreIFCGUID { get; private set; }
    public bool ExportVisibleElementsOnly { get; private set; }
    public bool SplitWallsAndColumns { get; private set; }

    public IFCExportWindow(List<(string Category, int Count)> categorySummary) : base("Ottawa Tools — IFCExportQA", minWidth: 420)
    {
        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🏗️", "IFC Export QA", $"Pre-flight: {categorySummary.Sum(c => c.Count)} element(s) across {categorySummary.Count} categories will be exported.", Close));

        var versionStack = new StackPanel();
        versionStack.Children.Add(OttawaWorkUi.FieldLabel("IFC version"));
        _versionBox.Items.Add("IFC2x3 Coordination View 2.0");
        _versionBox.Items.Add("IFC4");
        _versionBox.SelectedIndex = 0;
        versionStack.Children.Add(_versionBox);
        root.Children.Add(OttawaWorkUi.Card(versionStack));

        _advancedPanel = BuildAdvancedPanel();
        _advancedToggle.HorizontalAlignment = HorizontalAlignment.Left;
        _advancedToggle.Margin = new Thickness(0, 12, 0, 0);
        _advancedToggle.Click += (_, _) => ToggleAdvancedPanel();
        root.Children.Add(_advancedToggle);
        root.Children.Add(_advancedPanel);

        var summaryStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        summaryStack.Children.Add(OttawaWorkUi.SectionHeader("Categories"));
        var rowsStack = new StackPanel();
        foreach (var (category, count) in categorySummary.OrderByDescending(c => c.Count))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            row.Children.Add(new TextBlock { Text = category, FontSize = 12, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), Width = 260 });
            row.Children.Add(OttawaWorkUi.Badge(count.ToString()));
            rowsStack.Children.Add(row);
        }
        var scroll = new ScrollViewer { MaxHeight = 280, Content = rowsStack };
        summaryStack.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));
        root.Children.Add(summaryStack);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var exportButton = OttawaWorkUi.PrimaryButton("Export...");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        exportButton.Click += (_, _) =>
        {
            SelectedVersion = _versionBox.SelectedIndex == 1 ? IFCVersion.IFC4 : IFCVersion.IFC2x3CV2;
            CoordinateBase = _coordinateBaseBox.SelectedIndex switch
            {
                1 => "Project",
                2 => "Site",
                3 => "Shared",
                _ => "Internal",
            };
            ExportIFCCommonPropertySets = _commonPsetsCheck.IsChecked == true;
            ExportBaseQuantities = _baseQuantitiesCheck.IsChecked == true;
            ExportRevitPropertySets = _revitPsetsCheck.IsChecked == true;
            StoreIFCGUID = _storeGuidCheck.IsChecked == true;
            ExportVisibleElementsOnly = _visibleOnlyCheck.IsChecked == true;
            SplitWallsAndColumns = _splitWallsColumnsCheck.IsChecked == true;
            DialogResult = true;
            Close();
        };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(exportButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    /// <summary>Coordinate base dropdown + property-set/data toggles + geometry scope toggles, collapsed by default.</summary>
    private StackPanel BuildAdvancedPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };

        var coordStack = new StackPanel();
        coordStack.Children.Add(OttawaWorkUi.FieldLabel("Coordinate base"));
        _coordinateBaseBox.Items.Add("Internal Origin (Origin to Origin)");
        _coordinateBaseBox.Items.Add("Project Base Point");
        _coordinateBaseBox.Items.Add("Survey Point");
        _coordinateBaseBox.Items.Add("Shared Coordinates");
        _coordinateBaseBox.SelectedIndex = 0;
        coordStack.Children.Add(_coordinateBaseBox);

        var togglesStack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        togglesStack.Children.Add(OttawaWorkUi.SectionHeader("Property sets & data"));
        togglesStack.Children.Add(_commonPsetsCheck);
        togglesStack.Children.Add(_baseQuantitiesCheck);
        togglesStack.Children.Add(_revitPsetsCheck);
        togglesStack.Children.Add(_storeGuidCheck);

        var scopeStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        scopeStack.Children.Add(OttawaWorkUi.SectionHeader("Geometry & scope"));
        scopeStack.Children.Add(_visibleOnlyCheck);
        scopeStack.Children.Add(_splitWallsColumnsCheck);

        var inner = new StackPanel();
        inner.Children.Add(coordStack);
        inner.Children.Add(togglesStack);
        inner.Children.Add(scopeStack);
        stack.Children.Add(OttawaWorkUi.Card(inner));

        return stack;
    }

    private void ToggleAdvancedPanel()
    {
        var expanded = _advancedPanel.Visibility == Visibility.Visible;
        _advancedPanel.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        _advancedToggle.Content = expanded ? "▸ Advanced Settings" : "▾ Advanced Settings";
    }
}
