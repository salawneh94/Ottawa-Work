using StackPanel = System.Windows.Controls.StackPanel;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using CornerRadius = System.Windows.CornerRadius;
using TextTrimming = System.Windows.TextTrimming;

using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

/// <summary>
/// Shows one row per selected dimension segment with its current (model-driven)
/// value alongside editable Override/Prefix/Suffix fields, seeded from what's
/// already on the dimension. Mirrors OverriddenDimensionDetector's read side,
/// but lets you actually change what a dimension shows instead of just
/// flagging it. Used by both DimensionEditor (edits whatever's already
/// selected) and OverriddenDimensionDetector (edits whatever the scan
/// flagged) — moved here from DimensionEditor's own plugin folder so both
/// can reuse it without one plugin referencing another's assembly.
/// </summary>
public class DimensionEditorWindow : OttawaWorkWindow
{
    private readonly List<(DimensionEditRow Seed, TextBox OverrideBox, TextBox PrefixBox, TextBox SuffixBox)> _rows = new();

    public DimensionEditorWindow(List<DimensionEditRow> seeds) : base("Ottawa Tools — DimensionEditor", minWidth: 580)
    {
        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("📝", "DimensionEditor",
            $"{seeds.Count} dimension segment(s) selected — edit the override, prefix, or suffix, then click Apply. Leave Override blank to show the model-driven value again."));

        var rowsStack = new StackPanel();
        foreach (var seed in seeds)
        {
            var overrideBox = OttawaWorkUi.TextBox();
            overrideBox.Text = seed.Override;
            overrideBox.Width = 150;

            var prefixBox = OttawaWorkUi.TextBox();
            prefixBox.Text = seed.Prefix;
            prefixBox.Width = 100;

            var suffixBox = OttawaWorkUi.TextBox();
            suffixBox.Text = seed.Suffix;
            suffixBox.Width = 100;

            var header = new TextBlock
            {
                Text = seed.SegmentIndex is int segmentIndex
                    ? $"{seed.ViewName} — Dimension {seed.DimensionId} · Segment {segmentIndex} · currently \"{seed.CurrentValue}\""
                    : $"{seed.ViewName} — Dimension {seed.DimensionId} · currently \"{seed.CurrentValue}\"",
                FontSize = 12,
                Foreground = OttawaWorkUi.BrushOf(OttawaWorkUi.TextSecondary),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 6),
            };

            var fieldsRow = new StackPanel { Orientation = Orientation.Horizontal };
            fieldsRow.Children.Add(LabeledField("Override", overrideBox));
            fieldsRow.Children.Add(LabeledField("Prefix", prefixBox));
            fieldsRow.Children.Add(LabeledField("Suffix", suffixBox));

            var rowContent = new StackPanel();
            rowContent.Children.Add(header);
            rowContent.Children.Add(fieldsRow);

            var rowBorder = new Border
            {
                Background = OttawaWorkUi.BrushOf(OttawaWorkUi.CardBackgroundAlt),
                BorderBrush = OttawaWorkUi.BrushOf(OttawaWorkUi.BorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 2, 0, 2),
                Child = rowContent,
            };
            rowsStack.Children.Add(rowBorder);

            _rows.Add((seed, overrideBox, prefixBox, suffixBox));
        }

        var scroll = new ScrollViewer { MaxHeight = 400, Content = rowsStack };
        root.Children.Add(OttawaWorkUi.Card(scroll, padding: 8));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancelButton = OttawaWorkUi.SecondaryButton("Cancel");
        var applyButton = OttawaWorkUi.PrimaryButton("Apply");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        applyButton.Click += (_, _) => { DialogResult = true; Close(); };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(applyButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private static StackPanel LabeledField(string label, TextBox box)
    {
        box.Margin = new Thickness(0, 0, 12, 0);
        var stack = new StackPanel();
        stack.Children.Add(OttawaWorkUi.FieldLabel(label));
        stack.Children.Add(box);
        return stack;
    }

    public List<DimensionEditRow> BuildEdits()
    {
        return _rows
            .Select(r => r.Seed with { Override = r.OverrideBox.Text, Prefix = r.PrefixBox.Text, Suffix = r.SuffixBox.Text })
            .ToList();
    }
}
