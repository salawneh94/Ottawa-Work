using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

public enum DimensionOverrideSeverity { Falsified, Frozen, Annotated }

public record OverriddenDimensionRow(
    ElementId DimensionId,
    int? SegmentIndex,
    string ViewName,
    string DimTypeName,
    double? ActualValueFeet,
    string OverrideSummary,
    string OverrideText,
    DimensionOverrideSeverity Severity);

/// <summary>
/// Scans every Dimension in the model for a manual override (ValueOverride)
/// or annotation (Prefix/Suffix/Above/Below) and classifies what it finds —
/// the read/detect side of DimensionEditor's write side. Three severities,
/// built only from real Dimension/DimensionSegment API surface (Above/Below
/// confirmed to genuinely exist on both Dimension and DimensionSegment, not
/// assumed):
///   Falsified — ValueOverride is set AND, once parsed as a length the same
///     way Revit's own UI would parse typed input (UnitFormatUtils.TryParse),
///     formats (UnitFormatUtils.Format, same project units/precision) to a
///     DIFFERENT displayed string than the segment's real measured Value. A
///     fabricated number — the dimension is showing something other than
///     what it actually measures.
///   Frozen — ValueOverride is set but isn't provably a different number:
///     non-numeric override text (e.g. "EQ", "N.T.S.") that TryParse can't
///     turn into a length, or a numeric override that — once parsed and
///     reformatted the same way — actually matches the real value. Still
///     locked away from ever auto-updating if the model changes, just not
///     provably showing a false number today.
///   Annotated — no ValueOverride at all (still shows the real, model-
///     driven value), but has a Prefix/Suffix/Above/Below note attached.
///     Cosmetic, lowest risk.
/// A dimension/segment with none of these (no override, no annotation) is a
/// normal, fully model-driven dimension and isn't returned at all — only
/// what's actually flagged comes back, same as the reference tool's own
/// "here's what needs attention" behavior rather than listing everything.
/// </summary>
public static class OverriddenDimensionEngine
{
    public static List<OverriddenDimensionRow> Scan(Document doc)
    {
        var units = doc.GetUnits();
        var rows = new List<OverriddenDimensionRow>();

        var dimensions = new FilteredElementCollector(doc)
            .OfClass(typeof(Dimension))
            .Cast<Dimension>();

        foreach (var dimension in dimensions)
        {
            var ownerView = doc.GetElement(dimension.OwnerViewId) as View;
            var viewName = ownerView?.Name ?? "(unknown view)";
            var dimTypeName = dimension.DimensionType?.Name ?? "";

            if (dimension.NumberOfSegments > 1)
            {
                var index = 1;
                foreach (DimensionSegment segment in dimension.Segments)
                {
                    var row = BuildRow(units, dimension.Id, index, viewName, dimTypeName,
                        segment.Value, segment.ValueOverride, segment.Prefix, segment.Suffix, segment.Above, segment.Below);
                    if (row is not null) rows.Add(row);
                    index++;
                }
            }
            else
            {
                var row = BuildRow(units, dimension.Id, null, viewName, dimTypeName,
                    dimension.Value, dimension.ValueOverride, dimension.Prefix, dimension.Suffix, dimension.Above, dimension.Below);
                if (row is not null) rows.Add(row);
            }
        }

        return rows.OrderBy(r => SeverityRank(r.Severity)).ThenBy(r => r.ViewName).ToList();
    }

    private static int SeverityRank(DimensionOverrideSeverity severity) => severity switch
    {
        DimensionOverrideSeverity.Falsified => 0,
        DimensionOverrideSeverity.Frozen => 1,
        _ => 2,
    };

    private static OverriddenDimensionRow? BuildRow(
        Units units, ElementId dimensionId, int? segmentIndex, string viewName, string dimTypeName,
        double? actualValueFeet, string? overrideText, string? prefix, string? suffix, string? above, string? below)
    {
        var hasOverride = !string.IsNullOrWhiteSpace(overrideText);
        var hasAnnotation = !string.IsNullOrWhiteSpace(prefix) || !string.IsNullOrWhiteSpace(suffix)
            || !string.IsNullOrWhiteSpace(above) || !string.IsNullOrWhiteSpace(below);
        if (!hasOverride && !hasAnnotation) return null;

        var severity = DimensionOverrideSeverity.Annotated;
        if (hasOverride)
        {
            severity = DimensionOverrideSeverity.Frozen;
            if (actualValueFeet is { } actual
                && UnitFormatUtils.TryParse(units, SpecTypeId.Length, overrideText!, out var parsedOverride))
            {
                var actualFormatted = UnitFormatUtils.Format(units, SpecTypeId.Length, actual, false);
                var overrideFormatted = UnitFormatUtils.Format(units, SpecTypeId.Length, parsedOverride, false);
                if (actualFormatted != overrideFormatted)
                    severity = DimensionOverrideSeverity.Falsified;
            }
        }

        var summary = FormatOverrideSummary(overrideText, prefix, suffix, above, below);
        return new OverriddenDimensionRow(dimensionId, segmentIndex, viewName, dimTypeName, actualValueFeet, summary, overrideText ?? "", severity);
    }

    private static string FormatOverrideSummary(string? overrideText, string? prefix, string? suffix, string? above, string? below)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(overrideText)) parts.Add($"Value Override: {overrideText}");
        if (!string.IsNullOrWhiteSpace(prefix)) parts.Add($"Prefix: {prefix}");
        if (!string.IsNullOrWhiteSpace(suffix)) parts.Add($"Suffix: {suffix}");
        if (!string.IsNullOrWhiteSpace(above)) parts.Add($"Above: {above}");
        if (!string.IsNullOrWhiteSpace(below)) parts.Add($"Below: {below}");
        return string.Join("; ", parts);
    }
}
