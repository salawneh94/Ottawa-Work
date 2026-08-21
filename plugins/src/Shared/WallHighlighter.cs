using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

/// <summary>
/// Shared by HighlightExterior/HighlightInterior (the reference tool's
/// separate "HL Exterior"/"HL Interior" buttons) — the same toggle logic
/// HighlightWalls already uses for its combined interior+exterior pass,
/// scoped down to just one WallFunction at a time. State is detected by
/// checking whether the first matching wall's override already matches
/// this tool's own color, same as HighlightWalls, rather than tracked
/// separately (which could drift out of sync with manual V/G edits).
/// </summary>
public static class WallHighlighter
{
    public record Analysis(List<Wall> Walls, bool CurrentlyOn);

    public static Analysis Analyze(Document doc, View view, WallFunction function, Color color)
    {
        var walls = new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(Wall))
            .Cast<Wall>()
            .Where(w => (w.WallType?.Function ?? WallFunction.Interior) == function)
            .ToList();

        var currentlyOn = walls
            .Select(w => view.GetElementOverrides(w.Id).ProjectionLineColor)
            .Where(c => c.IsValid)
            .Any(c => ColorsEqual(c, color));

        return new Analysis(walls, currentlyOn);
    }

    public static void Apply(View view, Analysis analysis, Color color)
    {
        foreach (var wall in analysis.Walls)
        {
            if (analysis.CurrentlyOn)
            {
                view.SetElementOverrides(wall.Id, new OverrideGraphicSettings());
                continue;
            }

            var overrides = new OverrideGraphicSettings().SetProjectionLineColor(color).SetCutLineColor(color);
            view.SetElementOverrides(wall.Id, overrides);
        }
    }

    private static bool ColorsEqual(Color a, Color b) => a.Red == b.Red && a.Green == b.Green && a.Blue == b.Blue;
}
