using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace OttawaWork.Shared;

public record StairRuleResult(string ShortName, string RuleName, bool Pass, string Detail);

public record StairCombination(
    int RiserCount,
    double RiserMm,
    double TreadMm,
    double StepMeasureMm,
    double PitchDeg,
    double TotalRunMm,
    int Score,
    List<StairRuleResult> Rules);

public record StairAuditRow(
    ElementId StairId,
    string StairName,
    string LevelName,
    double RiserMm,
    double TreadMm,
    double? WidthMm,
    List<StairRuleResult> Rules);

/// <summary>
/// DIN 18065 ("Gebäudetreppen") reference thresholds plus a design-combination
/// search (for the Calculate mode) and an existing-stair auditor (for the
/// Audit mode) built on top of them. These are the well-known, commonly-cited
/// DIN 18065 numbers (the riser/tread ranges and the "Schrittmaßregel" 2s+a
/// step-measure formula) — not a substitute for checking the current published
/// standard and the project's own occupancy/Landesbauordnung requirements,
/// same "reference, not certified compliance" framing this tool already used
/// for its narrower 2R+G check.
/// </summary>
public static class StairCalculatorEngine
{
    public const double RiserMinMm = 140.0;
    public const double RiserMaxMm = 190.0;
    public const double TreadMinMm = 240.0;
    public const double StepMeasureMinMm = 590.0;
    public const double StepMeasureMaxMm = 650.0;
    public const double StepMeasureIdealMm = 630.0;
    public const double PitchMaxDeg = 45.0;
    public const double WidthMinMm = 800.0;

    public static List<StairRuleResult> EvaluateRules(double riserMm, double treadMm, double? widthMm)
    {
        var stepMeasure = 2 * riserMm + treadMm;
        var pitchDeg = RadiansToDegrees(Math.Atan2(riserMm, treadMm));

        var rules = new List<StairRuleResult>
        {
            new("R", "Steigung (riser) 140-190mm", riserMm >= RiserMinMm && riserMm <= RiserMaxMm, $"{riserMm:0} mm"),
            new("T", "Auftritt (tread) ≥240mm", treadMm >= TreadMinMm, $"{treadMm:0} mm"),
            new("S", "Schrittmaß 2s+a 590-650mm", stepMeasure >= StepMeasureMinMm && stepMeasure <= StepMeasureMaxMm, $"{stepMeasure:0} mm"),
            new("P", "Neigungswinkel ≤45°", pitchDeg <= PitchMaxDeg, $"{pitchDeg:0.0}°"),
        };
        if (widthMm is { } w)
            rules.Add(new StairRuleResult("W", "Nutzbare Laufbreite ≥800mm", w >= WidthMinMm, $"{w:0} mm"));

        return rules;
    }

    /// <summary>
    /// For a given total floor-to-floor rise, searches every riser count whose
    /// resulting riser height falls in the DIN 18065 range, derives the tread
    /// depth from the "Schrittmaßregel" (aiming for the 630mm ideal, floored at
    /// the 240mm tread minimum), and ranks the results — closer to the ideal
    /// step measure and fewer rule failures score higher; a combination whose
    /// total run doesn't fit the given available run is penalized rather than
    /// dropped outright, so a slightly-too-long option can still be
    /// seen/considered as "close" instead of silently vanishing.
    /// </summary>
    public static List<StairCombination> SearchCombinations(double floorToFloorMm, double availableRunMm, int maxResults = 8)
    {
        var results = new List<StairCombination>();
        if (floorToFloorMm <= 0) return results;

        var minRisers = (int)Math.Ceiling(floorToFloorMm / RiserMaxMm);
        var maxRisers = (int)Math.Floor(floorToFloorMm / RiserMinMm);
        if (maxRisers < minRisers) return results;

        for (var n = minRisers; n <= maxRisers; n++)
        {
            var riser = floorToFloorMm / n;
            var idealTread = StepMeasureIdealMm - 2 * riser;
            var tread = Math.Max(idealTread, TreadMinMm);
            var treadCount = n - 1;
            var totalRun = treadCount * tread;
            var fitsRun = availableRunMm <= 0 || totalRun <= availableRunMm;

            var rules = EvaluateRules(riser, tread, null);
            var score = ScoreCombination(riser, tread, rules, fitsRun);

            results.Add(new StairCombination(
                n, riser, tread, 2 * riser + tread,
                RadiansToDegrees(Math.Atan2(riser, tread)),
                totalRun, score, rules));
        }

        return results.OrderByDescending(c => c.Score).ThenBy(c => c.RiserCount).Take(maxResults).ToList();
    }

    private static int ScoreCombination(double riserMm, double treadMm, List<StairRuleResult> rules, bool fitsRun)
    {
        var score = 100;
        score -= rules.Count(r => !r.Pass) * 30;
        var stepMeasure = 2 * riserMm + treadMm;
        score -= (int)Math.Round(Math.Abs(stepMeasure - StepMeasureIdealMm) / 2.0);
        if (!fitsRun) score -= 50;
        return Math.Max(0, score);
    }

    /// <summary>
    /// Checks every real Stairs element already in the model against the same
    /// rules — Stairs.ActualRiserHeight/ActualTreadDepth are Revit's own
    /// calculated instance properties (already relied on before this
    /// rewrite), StairsRun.ActualRunWidth is read per run via
    /// Stairs.GetStairsRuns() and the narrowest run is used as the stair's
    /// width (the run that would fail a width check first), and
    /// Element.LevelId resolves the base level for display. A stair with no
    /// computed riser/tread yet (e.g. still mid-edit, or a non-standard
    /// component stair) is skipped rather than shown with a false 0mm value.
    /// </summary>
    public static List<StairAuditRow> AuditExistingStairs(Document doc)
    {
        var rows = new List<StairAuditRow>();

        var stairs = new FilteredElementCollector(doc)
            .OfClass(typeof(Stairs))
            .Cast<Stairs>();

        foreach (var stair in stairs)
        {
            var riserFeet = stair.ActualRiserHeight;
            var treadFeet = stair.ActualTreadDepth;
            if (riserFeet <= 0 || treadFeet <= 0) continue;

            var riserMm = UnitUtils.ConvertFromInternalUnits(riserFeet, UnitTypeId.Millimeters);
            var treadMm = UnitUtils.ConvertFromInternalUnits(treadFeet, UnitTypeId.Millimeters);

            double? widthMm = null;
            var runWidths = stair.GetStairsRuns()
                .Select(id => doc.GetElement(id) as StairsRun)
                .Where(run => run is not null)
                .Select(run => run!.ActualRunWidth)
                .Where(w => w > 0)
                .ToList();
            if (runWidths.Count > 0)
                widthMm = UnitUtils.ConvertFromInternalUnits(runWidths.Min(), UnitTypeId.Millimeters);

            var level = doc.GetElement(stair.LevelId) as Level;

            rows.Add(new StairAuditRow(
                stair.Id,
                string.IsNullOrWhiteSpace(stair.Name) ? $"Stair {stair.Id.Value}" : stair.Name,
                level?.Name ?? "",
                riserMm,
                treadMm,
                widthMm,
                EvaluateRules(riserMm, treadMm, widthMm)));
        }

        return rows;
    }

    private static double RadiansToDegrees(double radians) => radians * (180.0 / Math.PI);
}
