using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;

using BIMFlow.Shared;

namespace BIMFlow.IFCExportQA;

/// <summary>
/// Editable rule table (family/type name contains → IFC class) applied to
/// the built-in "Export Type to IFC As" parameter before export — seeded
/// with a real client mapping table so most projects just need to confirm
/// rather than build the list from scratch. "Skip" bypasses mapping
/// entirely for projects that don't need it, leaving today's plain export
/// unchanged.
/// </summary>
public class IfcClassMappingWindow : BimFlowWindow
{
    private readonly StackPanel _rowsPanel = new();
    private readonly List<(TextBox NameBox, TextBox ClassBox)> _rows = new();

    public bool Applied { get; private set; }

    public List<IfcClassMapper.Rule> Rules => _rows
        .Select(r => new IfcClassMapper.Rule(r.NameBox.Text.Trim(), r.ClassBox.Text.Trim()))
        .Where(r => r.NameContains.Length > 0 && r.IfcClass.Length > 0)
        .ToList();

    public IfcClassMappingWindow() : base("BIMFlow — IFC Class Mapping", minWidth: 480)
    {
        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🏷️", "IFC Class Mapping",
            "Family/type names containing the left text get their \"Export Type to IFC As\" parameter set to the class on the right, overriding Revit's default category mapping."));

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        headerRow.Children.Add(new TextBlock { Text = "NAME CONTAINS", FontSize = 10, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Width = 200 });
        headerRow.Children.Add(new TextBlock { Text = "IFC CLASS", FontSize = 10, Foreground = BimFlowUi.BrushOf(BimFlowUi.TextSecondary), Width = 200 });
        root.Children.Add(headerRow);

        var scroll = new ScrollViewer { MaxHeight = 340, Content = _rowsPanel };
        root.Children.Add(BimFlowUi.Card(scroll, padding: 8));

        foreach (var rule in IfcClassMapper.DefaultRules)
            AddRow(rule.NameContains, rule.IfcClass);

        var addRowButton = BimFlowUi.SecondaryButton("+ Add rule");
        addRowButton.Margin = new Thickness(0, 10, 0, 0);
        addRowButton.HorizontalAlignment = HorizontalAlignment.Left;
        addRowButton.Click += (_, _) => AddRow(string.Empty, string.Empty);
        root.Children.Add(addRowButton);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var skipButton = BimFlowUi.SecondaryButton("Skip");
        var applyButton = BimFlowUi.PrimaryButton("Apply & Continue");
        skipButton.Margin = new Thickness(0, 0, 10, 0);
        skipButton.Click += (_, _) => { Applied = false; DialogResult = true; Close(); };
        applyButton.Click += (_, _) => { Applied = true; DialogResult = true; Close(); };
        buttonRow.Children.Add(skipButton);
        buttonRow.Children.Add(applyButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private void AddRow(string nameContains, string ifcClass)
    {
        var nameBox = BimFlowUi.TextBox();
        nameBox.Width = 200;
        nameBox.Text = nameContains;

        var classBox = BimFlowUi.TextBox();
        classBox.Width = 200;
        classBox.Text = ifcClass;

        var removeButton = BimFlowUi.SecondaryButton("×");
        removeButton.Padding = new Thickness(8, 4, 8, 4);
        removeButton.Margin = new Thickness(6, 4, 0, 10);

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(nameBox);
        row.Children.Add(classBox);
        row.Children.Add(removeButton);

        _rows.Add((nameBox, classBox));
        removeButton.Click += (_, _) =>
        {
            _rowsPanel.Children.Remove(row);
            _rows.RemoveAll(r => r.NameBox == nameBox);
        };

        _rowsPanel.Children.Add(row);
    }
}
