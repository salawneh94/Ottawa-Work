using Button = System.Windows.Controls.Button;
using StackPanel = System.Windows.Controls.StackPanel;
using UniformGrid = System.Windows.Controls.Primitives.UniformGrid;
using Thickness = System.Windows.Thickness;

using OttawaWork.Shared;

namespace OttawaWork.HighlightDashboard;

/// <summary>
/// A card grid listing every Select/Highlight tool in Panel 1 — the "browse
/// everything" counterpart to the standalone quick-action buttons the same
/// panel also carries (Color Code, HL Exterior, HL Interior, PC Heatmap).
/// Clicking a card doesn't run any highlight/select logic itself: it just
/// records which tool was picked and closes, exactly like every other
/// Ottawa Tools window that hands a decision to Command.cs. Command.cs then
/// posts that tool's own already-registered command via
/// UIApplication.PostCommand — so every card runs through the exact same
/// Command class (and therefore the exact same Shared engine underneath:
/// WallHighlighter, OverrideByParamEngine, etc.) that its standalone
/// ribbon button already uses. Nothing about how any of these tools work
/// is duplicated or reimplemented here.
/// </summary>
public class HighlightDashboardWindow : OttawaWorkWindow
{
    public record DashboardEntry(string Icon, string Title, string Description, string CommandFullClassName);

    private static readonly DashboardEntry[] Entries =
    {
        new("🎨", "Color Code", "Color-code any category by any parameter value — pick a palette, preview live, then apply as real persistent view filters.", "OttawaWork.OverrideByParam.Command"),
        new("🧱", "HL Exterior", "Toggle a red color highlight on every exterior wall in the active view.", "OttawaWork.HighlightExterior.Command"),
        new("🧱", "HL Interior", "Toggle a blue color highlight on every interior wall in the active view.", "OttawaWork.HighlightInterior.Command"),
        new("☁️", "PC Color", "Tint point cloud instances with a preset or custom color, all in view or just the selection.", "OttawaWork.PointCloudColorizer.Command"),
        new("🌡️", "PC Heatmap", "Compare walls against point cloud scan data and color-code deviations: green/yellow/red by tolerance.", "OttawaWork.PointCloudHeatmap.Command"),
    };

    public string? SelectedCommandFullClassName { get; private set; }

    public HighlightDashboardWindow() : base("Ottawa Tools — Highlight", minWidth: 620)
    {
        var root = new StackPanel();
        root.Children.Add(OttawaWorkUi.TitleBar("🎯", "Highlight", "Every Select/Highlight tool in one place — pick one to run it."));

        var grid = new UniformGrid { Columns = 2 };
        foreach (var entry in Entries)
        {
            var card = OttawaWorkUi.CardButton(entry.Icon, entry.Title, entry.Description);
            var commandFullClassName = entry.CommandFullClassName;
            card.Click += (_, _) => Choose(commandFullClassName);
            grid.Children.Add(card);
        }
        root.Children.Add(grid);

        SetContent(root);
    }

    private void Choose(string commandFullClassName)
    {
        SelectedCommandFullClassName = commandFullClassName;
        DialogResult = true;
        Close();
    }
}
