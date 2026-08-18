using TextBox = System.Windows.Controls.TextBox;
using StackPanel = System.Windows.Controls.StackPanel;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Thickness = System.Windows.Thickness;
using FontFamily = System.Windows.Media.FontFamily;
using TextWrapping = System.Windows.TextWrapping;

using BIMFlow.Shared;

namespace BIMFlow.SharedParamSync;

/// <summary>Read-only diff report + Merge/Cancel — dark themed, replacing the old WinForms multiline TextBox dialog.</summary>
public class SharedParamDiffWindow : BimFlowWindow
{
    public SharedParamDiffWindow(string pathA, string pathB, DiffResult diff) : base("BIMFlow — Shared Param Sync", minWidth: 480)
    {
        var root = new StackPanel();
        root.Children.Add(BimFlowUi.TitleBar("🔀", "Shared Param Sync", "A always wins on conflicts — review the diff before merging."));

        var reportBox = new TextBox
        {
            Text = BuildReport(pathA, pathB, diff),
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Background = BimFlowUi.BrushOf(BimFlowUi.CardBackgroundAlt),
            Foreground = BimFlowUi.BrushOf(BimFlowUi.TextPrimary),
            BorderThickness = new Thickness(0),
        };
        var scroll = new ScrollViewer { MaxHeight = 380, Content = reportBox, HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto };
        root.Children.Add(BimFlowUi.Card(scroll, padding: 8));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelButton = BimFlowUi.SecondaryButton("Cancel");
        var mergeButton = BimFlowUi.PrimaryButton("Merge and save...");
        cancelButton.Margin = new Thickness(0, 0, 10, 0);
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        mergeButton.Click += (_, _) => { DialogResult = true; Close(); };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(mergeButton);
        root.Children.Add(buttonRow);

        SetContent(root);
    }

    private static string BuildReport(string pathA, string pathB, DiffResult diff)
    {
        var lines = new List<string>
        {
            $"File A: {pathA}",
            $"File B: {pathB}",
            "",
            $"Only in A: {diff.OnlyInA.Count}",
            $"Only in B: {diff.OnlyInB.Count}",
            $"Name conflicts (same name, different GUID): {diff.NameConflicts.Count}",
            $"GUID conflicts (same GUID, different name): {diff.GuidConflicts.Count}",
            "",
        };

        if (diff.NameConflicts.Count > 0)
        {
            lines.Add("== Name conflicts ==");
            lines.AddRange(diff.NameConflicts.Select(c => $"  \"{c.A.Name}\"  A={c.A.Guid}  B={c.B.Guid}"));
            lines.Add("");
        }

        if (diff.GuidConflicts.Count > 0)
        {
            lines.Add("== GUID conflicts ==");
            lines.AddRange(diff.GuidConflicts.Select(c => $"  {c.A.Guid}  A=\"{c.A.Name}\"  B=\"{c.B.Name}\""));
            lines.Add("");
        }

        lines.Add("Merging keeps every parameter from A as-is. Parameters from B are added");
        lines.Add("only when their GUID doesn't already exist in A — A always wins on conflicts.");

        return string.Join(Environment.NewLine, lines);
    }
}
