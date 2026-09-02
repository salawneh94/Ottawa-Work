using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

public enum LegendSizePreset { Compact, Standard, Large }

public record LegendValueRow(string Value, int Count, Color Color);

public record LegendStyleOptions(
    string Title,
    double TextSizeMm,
    double RowHeightMm,
    double SwatchSizeMm,
    double PaddingMm,
    double PlacementRightMm,
    double PlacementUpMm,
    bool ShowCount,
    bool HeaderFill,
    bool AltRows);

/// <summary>
/// The engine behind the color-coded parameter legend: scan a category
/// grouped by a chosen parameter's distinct values (with counts), assign
/// colors, and generate the actual legend-view content — filled-region
/// swatches (colored via a duplicated FilledRegionType per color, not view
/// overrides, since these are new elements this method itself creates, not
/// existing model elements shared across other views) plus text-note labels/
/// counts/title. Shares its value-grouping approach (ResolveGroupKey) with
/// OverrideByParam's "Color Code" tool — same underlying problem (group a
/// category's elements by a parameter's value) — but scans the WHOLE MODEL
/// rather than one view's visible set (a documentation legend describing
/// "every casework type in this project" needs to be view-independent,
/// unlike Color Code's view-specific override/filter use case), and doesn't
/// restrict to GetFilterableParametersInCommon (a legend only ever reads and
/// displays a value, it never needs to build a Revit ParameterFilterElement
/// rule from it, so that restriction — which exists purely for filter-rule
/// compatibility — doesn't apply here).
/// </summary>
public static class LegendBuilderEngine
{
    public static List<string> ParameterNames(Document doc, Category category)
    {
        var sample = new FilteredElementCollector(doc)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType()
            .FirstOrDefault();
        if (sample is null) return new List<string>();

        return sample.Parameters.Cast<Parameter>()
            .Select(p => p.Definition.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>Groups by the parameter's display text, falling back to the element's own TYPE if the
    /// instance-level Parameter comes back with no value — same fallback OverrideByParam's Color Code
    /// tool already needed for built-ins like Type Name, where the instance-level Parameter object is a
    /// hollow placeholder even though the type's own same-named parameter has the real value.</summary>
    private static string ResolveValue(Element element, string parameterName)
    {
        var display = element.LookupParameter(parameterName)?.AsValueString();
        if (!string.IsNullOrWhiteSpace(display)) return display;

        if (element.Document.GetElement(element.GetTypeId()) is { } typeElement)
        {
            display = typeElement.LookupParameter(parameterName)?.AsValueString();
            if (!string.IsNullOrWhiteSpace(display)) return display;
        }

        return "";
    }

    public static List<(string Value, int Count)> ScanValues(Document doc, Category category, string parameterName)
    {
        var elements = new FilteredElementCollector(doc)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType()
            .ToList();

        var counts = new Dictionary<string, int>();
        foreach (var element in elements)
        {
            var value = ResolveValue(element, parameterName);
            if (string.IsNullOrWhiteSpace(value)) continue;
            counts[value] = counts.GetValueOrDefault(value) + 1;
        }

        return counts.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public static (double SwatchMm, double RowMm, double TextMm, double PaddingMm) SizePreset(LegendSizePreset preset) => preset switch
    {
        LegendSizePreset.Compact => (12.0, 7.0, 1.8, 1.0),
        LegendSizePreset.Large => (24.0, 13.0, 3.0, 2.0),
        _ => (18.0, 10.0, 2.4, 1.5),
    };

    public static ElementId? FindSolidFillPatternId(Document doc) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(f => f.GetFillPattern() is { IsSolidFill: true, Target: FillPatternTarget.Drafting })
            ?.Id;

    /// <summary>Finds (or duplicates, on first use) a FilledRegionType carrying the given solid color —
    /// duplicated from whichever FilledRegionType the project already has (any one; only its color and
    /// pattern get overridden, not its line style), named so a repeat run reuses the same type instead of
    /// growing a new one every time. Colors an actual TYPE property (FilledRegionType.ForegroundPatternColor)
    /// rather than a per-view element override, since these are brand new elements this run itself creates
    /// — there's no "other view already shows this differently" concern a view-specific override would be
    /// solving, unlike the per-element overrides RoomPlanGenerator/OverrideByParam use elsewhere in this
    /// codebase for EXISTING model elements shared across views.</summary>
    public static ElementId FindOrCreateSwatchType(Document doc, Color color)
    {
        var name = $"Ottawa Legend {color.Red}-{color.Green}-{color.Blue}";
        var existing = new FilteredElementCollector(doc)
            .OfClass(typeof(FilledRegionType))
            .Cast<FilledRegionType>()
            .FirstOrDefault(t => t.Name == name);
        if (existing is not null) return existing.Id;

        var baseType = new FilteredElementCollector(doc)
            .OfClass(typeof(FilledRegionType))
            .Cast<FilledRegionType>()
            .FirstOrDefault();
        if (baseType is null) throw new InvalidOperationException("No filled region type found in this project — Revit's default template always ships at least one, so this project's must have been deleted.");

        var newType = (FilledRegionType)baseType.Duplicate(name);
        newType.ForegroundPatternColor = color;
        newType.BackgroundPatternColor = color;

        var solidPatternId = FindSolidFillPatternId(doc);
        if (solidPatternId is not null)
        {
            newType.ForegroundPatternId = solidPatternId;
            newType.BackgroundPatternId = solidPatternId;
        }

        return newType.Id;
    }

    /// <summary>Finds (or duplicates, on first use) a TextNoteType at the given paper text size — same
    /// find-or-duplicate-and-cache-by-name pattern as FindOrCreateSwatchType. TEXT_SIZE is the
    /// BuiltInParameter backing a text type's font size; TextNoteType itself exposes no direct .TextSize
    /// property (confirmed via reflection — everything about it is parameter-based, unlike
    /// FilledRegionType's own ForegroundPatternColor property).</summary>
    private static ElementId FindOrCreateTextType(Document doc, double textSizeMm)
    {
        var name = $"Ottawa Legend {textSizeMm:0.0}mm";
        var existing = new FilteredElementCollector(doc)
            .OfClass(typeof(TextNoteType))
            .Cast<TextNoteType>()
            .FirstOrDefault(t => t.Name == name);
        if (existing is not null) return existing.Id;

        var baseType = doc.GetElement(doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType)) as TextNoteType
            ?? new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().FirstOrDefault();
        if (baseType is null) throw new InvalidOperationException("No text note type found in this project.");

        var newType = (TextNoteType)baseType.Duplicate(name);
        var sizeParam = newType.get_Parameter(BuiltInParameter.TEXT_SIZE);
        if (sizeParam is not null && !sizeParam.IsReadOnly)
            sizeParam.Set(UnitUtils.ConvertToInternalUnits(textSizeMm, UnitTypeId.Millimeters));

        return newType.Id;
    }

    private static CurveLoop RectangleLoop(XYZ topLeft, double width, double height)
    {
        var p1 = topLeft;
        var p2 = new XYZ(topLeft.X + width, topLeft.Y, 0);
        var p3 = new XYZ(topLeft.X + width, topLeft.Y - height, 0);
        var p4 = new XYZ(topLeft.X, topLeft.Y - height, 0);
        return CurveLoop.Create(new List<Curve> { Line.CreateBound(p1, p2), Line.CreateBound(p2, p3), Line.CreateBound(p3, p4), Line.CreateBound(p4, p1) });
    }

    /// <summary>Builds every swatch/label/title/fill element for the legend, top to bottom. Row width for
    /// the header-fill/alt-row bands is a heuristic (not measured — Revit has no simple "how wide will this
    /// TextNote render" query without creating it first) based on the longest value's character count, not
    /// pixel-exact.
    ///
    /// Every model-space distance (swatch size, row spacing, padding, placement offset) is multiplied by
    /// the legend view's own Scale before converting mm to internal feet — confirmed live (user-reported,
    /// screenshot showed every row's swatch and text collapsed on top of each other into one unreadable
    /// smear). Root cause: FilledRegion geometry is real model-space geometry, which prints at
    /// (model distance) / (view scale) — exactly like a wall drawn 1m long prints smaller on a 1:50 view
    /// than a 1:10 one — but TextNote's own font size (TEXT_SIZE) is a fixed PAPER size that does NOT
    /// scale with the view, by design (so annotation text reads the same size on the sheet regardless of
    /// which scale the underlying view happens to be drawn at). The style values here are meant as "this
    /// many mm on the printed sheet", so the GEOMETRY has to be inflated by the view's scale factor to
    /// still measure that many mm once scaled back down at print time; the TEXT SIZE itself must NOT get
    /// that same multiplication, since it already prints at its real, unscaled size.</summary>
    public static void GenerateLegendContent(Document doc, View legendView, LegendStyleOptions style, List<LegendValueRow> rows)
    {
        var textTypeId = FindOrCreateTextType(doc, style.TextSizeMm);
        var scale = Math.Max(1, legendView.Scale);
        double Feet(double mm) => UnitUtils.ConvertToInternalUnits(mm * scale, UnitTypeId.Millimeters);

        var originX = Feet(style.PlacementRightMm);
        var originY = -Feet(style.PlacementUpMm);
        var swatchWidth = Feet(style.SwatchSizeMm);
        var rowHeight = Feet(style.RowHeightMm);
        // Capped to a fraction of rowHeight, not just swatchWidth * 0.7: SwatchSizeMm and RowHeightMm are
        // independent style sliders, and swatchWidth * 0.7 alone comes out taller than rowHeight at every
        // size preset (e.g. Standard: 18mm swatch -> 12.6mm tall vs. a 10mm row) — confirmed live (user
        // screenshot showed swatches overlapping into the row above/below, the overlapping boundary lines
        // reading as stray tick marks, and the alt-row shading looking like it spanned two rows). Capping
        // keeps the swatch inside its own row regardless of what the width slider is set to.
        var swatchHeight = Math.Min(swatchWidth * 0.7, rowHeight * 0.82);
        var padding = Feet(style.PaddingMm);

        var longestLabel = rows.Count == 0 ? 10 : rows.Max(r => r.Value.Length + (style.ShowCount ? 8 : 0));
        var rowWidth = swatchWidth + padding + Feet(longestLabel * 2.2);

        var y = originY;

        if (!string.IsNullOrWhiteSpace(style.Title) && textTypeId != ElementId.InvalidElementId)
        {
            if (style.HeaderFill)
            {
                var headerColor = new Color(37, 99, 235);
                var headerTypeId = FindOrCreateSwatchType(doc, headerColor);
                var headerLoop = RectangleLoop(new XYZ(originX, y, 0), rowWidth, rowHeight * 1.3);
                FilledRegion.Create(doc, headerTypeId, legendView.Id, new List<CurveLoop> { headerLoop });
            }
            TextNote.Create(doc, legendView.Id, new XYZ(originX + padding, y - rowHeight * 0.15, 0), style.Title, textTypeId);
            y -= rowHeight * 1.4;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            if (style.AltRows && i % 2 == 1)
            {
                var altColor = new Color(235, 235, 240);
                var altTypeId = FindOrCreateSwatchType(doc, altColor);
                var altLoop = RectangleLoop(new XYZ(originX, y, 0), rowWidth, rowHeight);
                FilledRegion.Create(doc, altTypeId, legendView.Id, new List<CurveLoop> { altLoop });
            }

            var swatchTypeId = FindOrCreateSwatchType(doc, row.Color);
            var swatchTop = new XYZ(originX + padding / 2, y - (rowHeight - swatchHeight) / 2, 0);
            var swatchLoop = RectangleLoop(swatchTop, swatchWidth, swatchHeight);
            FilledRegion.Create(doc, swatchTypeId, legendView.Id, new List<CurveLoop> { swatchLoop });

            if (textTypeId != ElementId.InvalidElementId)
            {
                var labelText = style.ShowCount ? $"{row.Value}  ({row.Count})" : row.Value;
                var labelPosition = new XYZ(originX + padding / 2 + swatchWidth + padding, y - (rowHeight - swatchHeight) / 2, 0);
                TextNote.Create(doc, legendView.Id, labelPosition, labelText, textTypeId);
            }

            y -= rowHeight;
        }
    }
}
