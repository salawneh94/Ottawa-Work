using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;
using System.Windows.Forms;
// UseWPF+UseWindowsForms together drops System.IO from implicit global
// usings (see Shared/LicenseStore.cs) — needed here for Path.
using Path = System.IO.Path;

namespace OttawaWork.OverrideByParam;

/// <summary>
/// Colors elements of a chosen category in the active view by a chosen
/// parameter's value, applied as real persistent ParameterFilterElement-
/// based view filters (editable later in Visibility/Graphics, unlike a
/// one-off per-element OverrideGraphicSettings) — the general-purpose
/// version of color-by-value that isn't locked to MEP systems the way
/// SystemColorCoder is. Also exports the colored view as PNG, and can
/// clear the filters this tool created.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    // No colon, no equals sign — every prior fix sanitized only the DYNAMIC
    // pieces of the filter name (category, parameter, value) and left this
    // static template's own ": "/" = " glue untouched, on the assumption
    // colon was obviously safe since it's not in Revit's own (non-exhaustive
    // — the message says "such as") example list. It wasn't a safe
    // assumption: confirmed live across three separate reports, EVERY
    // single "Apply as filters" attempt failed 100% of the time regardless
    // of how different the actual category/parameter/value content was
    // each time (English, German, umlauts, plain numbers, family names with
    // hyphens/underscores already in them) — the one thing that never
    // changed across every failure was this template's own colons and
    // equals sign. Colon is part of Revit's well-documented official
    // name-restriction set even though the runtime message doesn't spell
    // it out. Hyphen and space are unambiguously safe (ordinary Revit view/
    // filter names use them constantly), so those are the only glue
    // characters left.
    private const string FilterPrefix = "Ottawa Tools - ";

    protected override string PluginSlug => "overridebyparam";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var window = new OverrideByParamWindow(doc, view);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        return window.ChosenAction switch
        {
            ColorCodeAction.Preview => Preview(doc, view, window),
            ColorCodeAction.ApplyFilters => ApplyFilters(doc, view, window),
            ColorCodeAction.ExportPng => ExportPng(doc, view),
            ColorCodeAction.ClearFilters => ClearFilters(doc, view),
            _ => Result.Succeeded,
        };
    }

    /// <summary>
    /// Revit rejects a ParameterFilterElement name containing characters
    /// like {}[]|;&lt;&gt;?`~ — confirmed live (user-reported), three times
    /// now. Round 1 only stripped that exact list Revit's own error message
    /// happened to spell out (non-exhaustive — the message says "such as").
    /// Round 2 switched to allowlisting letters/digits/whitespace/common
    /// punctuation instead, which should have covered anything printable —
    /// but it still crashed, on "1.1 ROHBAU", which is visibly nothing but
    /// ASCII letters, digits, a period, and spaces. The remaining suspect:
    /// char.IsWhiteSpace(c) is true for far more than the plain space bar —
    /// U+00A0 (non-breaking space) included — and a DIN-276-style cost code
    /// copy-pasted out of an official PDF table commonly carries exactly
    /// that, indistinguishable from a normal space to the eye. Only the
    /// literal ASCII space (U+0020) is allowed through now; every other
    /// Unicode whitespace variant gets replaced like any other unlisted
    /// character. Letters keep the broad char.IsLetterOrDigit allowance
    /// (any script, not just ASCII, since German umlauts etc. must stay
    /// legible) — this is specifically about whitespace look-alikes, not
    /// about narrowing the letter/digit allowance. See also ApplyFilters
    /// below, which no longer trusts sanitization alone to prevent every
    /// possible failure — each value's filter creation has its own
    /// try/catch too, so a still-unknown edge case reports which value and
    /// why instead of silently crashing the whole batch again.
    /// </summary>
    private static string SanitizeFilterNameSegment(string text)
    {
        const string allowedPunctuation = "-_.,()&+/";
        var sanitized = new string(text.Select(c =>
            char.IsLetterOrDigit(c) || c == ' ' || allowedPunctuation.Contains(c) ? c : '_').ToArray());
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }

    // Internal-units tolerance for Double equality — Revit's own values are
    // never exactly round after unit conversion, so an exact == comparison
    // would silently match nothing; this is the same order-of-magnitude
    // tolerance Autodesk's own SDK samples use for CreateEqualsRule.
    private const double DoubleEpsilon = 1e-6;

    /// <summary>
    /// Dispatches to the correctly-typed ParameterFilterRuleFactory overload
    /// for the target parameter's real StorageType, using the raw value
    /// OverrideByParamWindow captured when building the legend (see
    /// ResolveGroupKey there) rather than re-deriving it from display text.
    /// Building every rule as the String overload — what this used to do
    /// unconditionally — is exactly what made Family/Type/Level-style
    /// ElementId-storage parameters fail with "does not apply to this
    /// filter's categories": that message isn't really about the category
    /// at all when the real problem is a type mismatch between the rule and
    /// the parameter it's being evaluated against. Falls back to the string
    /// overload only if no typed raw value was captured (shouldn't normally
    /// happen for a value that reached this point at all).
    /// </summary>
    private static FilterRule CreateEqualsRule(ElementId parameterId, object? rawValue, string displayValue) => rawValue switch
    {
        ElementId eid => ParameterFilterRuleFactory.CreateEqualsRule(parameterId, eid),
        int i => ParameterFilterRuleFactory.CreateEqualsRule(parameterId, i),
        double d => ParameterFilterRuleFactory.CreateEqualsRule(parameterId, d, DoubleEpsilon),
        _ => ParameterFilterRuleFactory.CreateEqualsRule(parameterId, displayValue),
    };

    /// <summary>
    /// The built-in "Solid fill" drafting pattern, found structurally
    /// (FillPattern.IsSolidFill, on the Drafting target Revit's own view-
    /// graphic overrides use) rather than by name — a name-based lookup
    /// (GetFillPatternElementByName(doc, target, "Solid fill")) would hit
    /// the exact same locale bug already found and fixed once this session
    /// for Bulk Set's hardcoded English field names: on a non-English
    /// Revit install, "Solid fill" isn't the pattern's real display name.
    /// Null only if a project's Drafting patterns were somehow deleted
    /// entirely, which BuildOverrides treats as "line color only" rather
    /// than throwing.
    /// </summary>
    private static ElementId? FindSolidFillPatternId(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(f => f.GetFillPattern() is { IsSolidFill: true, Target: FillPatternTarget.Drafting })
            ?.Id;
    }

    /// <summary>
    /// Confirmed live (user-reported): filters were created and enabled
    /// correctly (visible with the right line-color swatches in Visibility/
    /// Graphics → Filters) but "no door got the color changed" — because
    /// this only ever set the PROJECTION/CUT LINE color, never the surface
    /// fill. A door in plan view is mostly thin lines with no fill pattern
    /// by default, so a line-only color change is easy to miss entirely —
    /// this now also sets a solid surface/cut fill in the same color
    /// (skipped gracefully if no Drafting solid-fill pattern is found, see
    /// FindSolidFillPatternId), which is what actually reads as "this
    /// element is colored" the way the reference tool's own screenshots do.
    /// </summary>
    private static OverrideGraphicSettings BuildOverrides(Autodesk.Revit.DB.Color color, int transparency, ColorCodeMode mode, ElementId? solidFillPatternId)
    {
        var overrides = new OverrideGraphicSettings().SetProjectionLineColor(color).SetCutLineColor(color);

        if (solidFillPatternId is not null)
        {
            overrides = overrides
                .SetSurfaceForegroundPatternId(solidFillPatternId)
                .SetSurfaceForegroundPatternColor(color)
                .SetSurfaceForegroundPatternVisible(true)
                .SetCutForegroundPatternId(solidFillPatternId)
                .SetCutForegroundPatternColor(color)
                .SetCutForegroundPatternVisible(true);
        }

        if (mode == ColorCodeMode.ColorAndTransparency) overrides = overrides.SetSurfaceTransparency(transparency);
        return overrides;
    }

    private Result Preview(Document doc, View view, OverrideByParamWindow window)
    {
        if (window.SelectedCategory is null || window.SelectedParameterName is null)
            return Result.Succeeded;

        var category = window.SelectedCategory;
        var paramName = window.SelectedParameterName;

        var elements = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .OfCategoryId(category.Id)
            .ToList();

        var solidFillPatternId = FindSolidFillPatternId(doc);

        using var transaction = new Transaction(doc, "Ottawa Tools: Preview Color Code");
        transaction.Start();
        try
        {
            if (window.WipeFirst)
                foreach (var element in elements)
                    view.SetElementOverrides(element.Id, new OverrideGraphicSettings());

            var applied = 0;
            foreach (var element in elements)
            {
                var value = element.LookupParameter(paramName)?.AsValueString();
                var key = string.IsNullOrWhiteSpace(value) ? OverrideByParamWindow.NoValueKey : value!;
                if (key == OverrideByParamWindow.NoValueKey) continue;
                if (!window.SelectedValues.Contains(key)) continue;

                var overrides = BuildOverrides(window.ColorByValue[key], window.Transparency, window.Mode, solidFillPatternId);
                view.SetElementOverrides(element.Id, overrides);
                applied++;
            }

            transaction.Commit();
            TaskDialog.Show("Ottawa Tools — Color Code", $"Previewed {applied} element(s) directly in the active view. Nothing was saved as a filter — use \"Apply as filters\" to make it persistent, or re-run Preview/Clear to change it.");
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("Ottawa Tools — Color Code", $"Couldn't preview: {ex.Message}");
            return Result.Failed;
        }

        return Result.Succeeded;
    }

    private Result ApplyFilters(Document doc, View view, OverrideByParamWindow window)
    {
        if (window.SelectedCategory is null || window.SelectedParameterName is null)
            return Result.Succeeded;

        var category = window.SelectedCategory;
        var paramName = window.SelectedParameterName;

        var sample = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .OfCategoryId(category.Id)
            .Select(e => e.LookupParameter(paramName))
            .FirstOrDefault(p => p is not null);

        if (sample is null)
        {
            TaskDialog.Show("Ottawa Tools — Color Code", "Couldn't find that parameter on any element of this category in the active view.");
            return Result.Cancelled;
        }

        var namePrefix = $"{FilterPrefix}{SanitizeFilterNameSegment(category.Name)} - {SanitizeFilterNameSegment(paramName)} - ";
        var solidFillPatternId = FindSolidFillPatternId(doc);

        using var transaction = new Transaction(doc, "Ottawa Tools: Apply Color Code Filters");
        transaction.Start();
        try
        {
            if (window.WipeFirst)
            {
                foreach (var filterId in view.GetFilters().ToList())
                {
                    if (doc.GetElement(filterId) is ParameterFilterElement pfe && pfe.Name.StartsWith(namePrefix, StringComparison.Ordinal))
                        view.RemoveFilter(filterId);
                }
            }

            var applied = 0;
            var failures = new List<string>();
            foreach (var value in window.SelectedValues.Where(v => v != OverrideByParamWindow.NoValueKey))
            {
                // Sanitizing the name is a best-effort guard, not a
                // guarantee — Revit's own "prohibited characters" error
                // message says "such as", i.e. its example list isn't
                // exhaustive, so a value can still get rejected for a
                // reason SanitizeFilterNameSegment didn't anticipate. Each
                // value gets its own try/catch so one such failure doesn't
                // take every other (perfectly fine) value down with it —
                // the old all-in-one-try version is exactly what turned a
                // single bad value into a total crash, confirmed live twice.
                try
                {
                    var filterName = namePrefix + SanitizeFilterNameSegment(value);
                    var rule = CreateEqualsRule(sample.Id, window.RawValueByKey.GetValueOrDefault(value), value);
                    var elementFilter = new ElementParameterFilter(rule);

                    var existing = new FilteredElementCollector(doc)
                        .OfClass(typeof(ParameterFilterElement))
                        .Cast<ParameterFilterElement>()
                        .FirstOrDefault(f => f.Name == filterName);

                    var pfe = existing;
                    if (pfe is null)
                        pfe = ParameterFilterElement.Create(doc, filterName, new List<ElementId> { category.Id }, elementFilter);
                    else
                        pfe.SetElementFilter(elementFilter);

                    if (!view.GetFilters().Contains(pfe.Id))
                        view.AddFilter(pfe.Id);

                    var overrides = BuildOverrides(window.ColorByValue[value], window.Transparency, window.Mode, solidFillPatternId);
                    view.SetFilterOverrides(pfe.Id, overrides);
                    applied++;
                }
                catch (Exception ex)
                {
                    failures.Add($"\"{value}\": {ex.Message}");
                }
            }

            transaction.Commit();

            var summary = $"Applied {applied} of {applied + failures.Count} filter(s) for \"{paramName}\" on {category.Name} to the active view.";
            if (failures.Count > 0)
                summary += $"\n\n{failures.Count} value(s) couldn't be applied:\n{string.Join("\n", failures)}";
            TaskDialog.Show("Ottawa Tools — Color Code", summary);
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("Ottawa Tools — Color Code", $"Couldn't apply filters: {ex.Message}");
            return Result.Failed;
        }

        return Result.Succeeded;
    }

    private Result ExportPng(Document doc, View view)
    {
        using var saveDialog = new SaveFileDialog
        {
            Title = "Export view as PNG",
            Filter = "PNG image (*.png)|*.png",
            FileName = $"{view.Name}.png",
        };
        if (saveDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

        var folder = Path.GetDirectoryName(saveDialog.FileName)!;
        var fileNameOnly = Path.GetFileNameWithoutExtension(saveDialog.FileName);

        var options = new ImageExportOptions
        {
            ExportRange = ExportRange.SetOfViews,
            FilePath = Path.Combine(folder, fileNameOnly),
            HLRandWFViewsFileType = ImageFileType.PNG,
            ShadowViewsFileType = ImageFileType.PNG,
            ZoomType = ZoomFitType.FitToPage,
            PixelSize = 2048,
            ImageResolution = ImageResolution.DPI_150,
        };
        options.SetViewsAndSheets(new List<ElementId> { view.Id });

        try
        {
            doc.ExportImage(options);
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Ottawa Tools — Color Code", $"Export failed: {ex.Message}");
            return Result.Failed;
        }

        TaskDialog.Show("Ottawa Tools — Color Code", $"Exported the active view to:\n{folder}\n\n(Revit names the file after the view; check that folder if \"{fileNameOnly}.png\" isn't there exactly.)");
        return Result.Succeeded;
    }

    private Result ClearFilters(Document doc, View view)
    {
        using var transaction = new Transaction(doc, "Ottawa Tools: Clear Color Code Filters");
        transaction.Start();
        var removed = 0;
        try
        {
            foreach (var filterId in view.GetFilters().ToList())
            {
                if (doc.GetElement(filterId) is ParameterFilterElement pfe && pfe.Name.StartsWith(FilterPrefix, StringComparison.Ordinal))
                {
                    view.RemoveFilter(filterId);
                    removed++;
                }
            }
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("Ottawa Tools — Color Code", $"Couldn't clear filters: {ex.Message}");
            return Result.Failed;
        }

        TaskDialog.Show("Ottawa Tools — Color Code", $"Removed {removed} Ottawa Tools filter(s) from the active view.");
        return Result.Succeeded;
    }
}
