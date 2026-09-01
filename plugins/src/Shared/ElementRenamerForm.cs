using StackPanel = System.Windows.Controls.StackPanel;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using CheckBox = System.Windows.Controls.CheckBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Thickness = System.Windows.Thickness;
using FontWeights = System.Windows.FontWeights;
using RoutedEventArgs = System.Windows.RoutedEventArgs;
using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;

using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

public record ElementRenamePlan(Element Element, string NewName);

/// <summary>
/// Batch rename dialog with a live preview and collision detection, generic
/// over any named Element (views, grids, levels, ...). Currently used by
/// GridRenumber. Dark themed — replaces the old WinForms DataGridView
/// dialog with a scrollable list of rows built once and refreshed in place
/// as the rules change.
/// </summary>
public class ElementRenamerForm : OttawaWorkWindow
{
    private readonly List<Element> _elements;
    private readonly List<(CheckBox Include, Element Element, TextBlock Preview)> _rows = new();
    private readonly TextBox _findBox = OttawaWorkUi.TextBox();
    private readonly TextBox _replaceBox = OttawaWorkUi.TextBox();
    private readonly TextBox _prefixBox = OttawaWorkUi.TextBox();
    private readonly TextBox _suffixBox = OttawaWorkUi.TextBox();
    private readonly CheckBox _regexCheck;
    private readonly TextBlock _statusLabel;
    private readonly System.Windows.Controls.Button _renameButton;

    public ElementRenamerForm(string title, List<Element> elements) : base(title, minWidth: 520)
    {
        _elements = elements;

        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("✏️", title, $"{elements.Count} element(s) — set a rename rule and review the preview below."));

        var rulesStack = new StackPanel();
        var row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        row1.Children.Add(LabeledField("Find", _findBox));
        row1.Children.Add(LabeledField("Replace", _replaceBox));
        rulesStack.Children.Add(row1);

        var row2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        row2.Children.Add(LabeledField("Prefix", _prefixBox));
        row2.Children.Add(LabeledField("Suffix", _suffixBox));
        rulesStack.Children.Add(row2);

        _regexCheck = OttawaWorkUi.CheckBoxItem("Use regular expression");
        rulesStack.Children.Add(_regexCheck);
        root.Children.Add(OttawaWorkUi.Card(rulesStack));

        foreach (var box in new[] { _findBox, _replaceBox, _prefixBox, _suffixBox })
        {
            box.Width = 200;
            box.TextChanged += (_, _) => RefreshPreview();
        }
        _regexCheck.Checked += (_, _) => RefreshPreview();
        _regexCheck.Unchecked += (_, _) => RefreshPreview();

        var listStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        listStack.Children.Add(OttawaWorkUi.SectionHeader("Preview"));

        var quickRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var selectAllButton = OttawaWorkUi.SecondaryButton("Select all");
        var selectNoneButton = OttawaWorkUi.SecondaryButton("Select none");
        selectAllButton.Margin = new Thickness(0, 0, 6, 0);
        selectAllButton.Click += (_, _) => { foreach (var (include, _, _) in _rows) include.IsChecked = true; RefreshPreview(); };
        selectNoneButton.Click += (_, _) => { foreach (var (include, _, _) in _rows) include.IsChecked = false; RefreshPreview(); };
        quickRow.Children.Add(selectAllButton);
        quickRow.Children.Add(selectNoneButton);
        listStack.Children.Add(quickRow);

        var rowsStack = new StackPanel();
        foreach (var element in elements)
        {
            var includeBox = OttawaWorkUi.CheckBoxItem("", isChecked: true);
            var original = new TextBlock { Text = element.Name, FontSize = 12, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Width = 180, VerticalAlignment = VerticalAlignment.Center };
            var arrow = new TextBlock { Text = "→", FontSize = 12, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            var preview = new TextBlock { Text = element.Name, FontSize = 12, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextPrimary), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };

            var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            rowPanel.Children.Add(includeBox);
            rowPanel.Children.Add(original);
            rowPanel.Children.Add(arrow);
            rowPanel.Children.Add(preview);
            rowsStack.Children.Add(rowPanel);

            includeBox.Checked += (_, _) => RefreshPreview();
            includeBox.Unchecked += (_, _) => RefreshPreview();

            _rows.Add((includeBox, element, preview));
        }
        var scroll = new ScrollViewer { MaxHeight = 300, Content = rowsStack };
        listStack.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));
        root.Children.Add(listStack);

        _statusLabel = new TextBlock { FontSize = 11, Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary), Margin = new Thickness(0, 10, 0, 0) };
        root.Children.Add(_statusLabel);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        _renameButton = OttawaWorkUi.PrimaryButton("Rename");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        _renameButton.Click += (_, _) => { DialogResult = true; Close(); };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(_renameButton);
        root.Children.Add(buttonRow);

        SetContent(root);
        RefreshPreview();
    }

    private static StackPanel LabeledField(string label, TextBox box)
    {
        box.Width = 200;
        box.Margin = new Thickness(0, 0, 16, 0);
        var stack = new StackPanel();
        stack.Children.Add(OttawaWorkUi.FieldLabel(label));
        stack.Children.Add(box);
        return stack;
    }

    private void RefreshPreview()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasCollision = false;

        foreach (var (include, element, preview) in _rows)
        {
            string previewText;
            try
            {
                previewText = include.IsChecked == true
                    ? RenameRules.Apply(element.Name, _findBox.Text, _replaceBox.Text, _prefixBox.Text, _suffixBox.Text, _regexCheck.IsChecked == true)
                    : element.Name;
            }
            catch (Exception)
            {
                previewText = element.Name; // invalid regex mid-typing — show original until it's valid
            }

            preview.Text = previewText;

            if (include.IsChecked == true && !seen.Add(previewText))
                hasCollision = true;
        }

        if (hasCollision)
        {
            _statusLabel.Text = "Some new names collide — adjust the rule before renaming.";
            _statusLabel.Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.Danger);
            _renameButton.IsEnabled = false;
        }
        else
        {
            var count = _rows.Count(r => r.Include.IsChecked == true);
            _statusLabel.Text = $"{count} element(s) will be renamed.";
            _statusLabel.Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary);
            _renameButton.IsEnabled = true;
        }
    }

    public List<ElementRenamePlan> BuildRenamePlan()
    {
        var plan = new List<ElementRenamePlan>();
        foreach (var (include, element, preview) in _rows)
        {
            if (include.IsChecked != true) continue;
            if (preview.Text != element.Name)
                plan.Add(new ElementRenamePlan(element, preview.Text));
        }
        return plan;
    }
}
