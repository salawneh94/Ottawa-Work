using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

public readonly record struct ParamOpResult(int Applied, int Skipped, int Failed);

public record FamilyTypeGroup(string FamilyName, string TypeName, int Count);

public record ParamChangePreview(ElementId ElementId, string ElementLabel, string OldValue, string NewValue);

public enum CaseMode { Upper, Lower, Title }

public enum CopyMode { Overwrite, Append }

/// <summary>
/// Pure-logic engine behind every tab of the Param Power Suite: element
/// loading/grouping (shared left sidebar) plus the four "compute a preview,
/// then write it" operations — Find/Replace, Case Transform, Copy A→B, and
/// Combine (Tokens) — which all resolve to the same shape ("write this
/// exact string onto this element's target parameter") and so share one
/// ApplyChanges. Bulk Set doesn't fit that shape (many parameters at once,
/// no single old/new pair) and gets its own Apply method.
///
/// Every Preview* method only reads the model, never writes — so every tab
/// can show what WOULD change before anything is touched. Apply/ApplyChanges
/// do the actual Parameter writes and are only ever called from inside a
/// Transaction opened by ParamPowerSuiteWindow itself (see that class's doc
/// comment for why this tool departs from every other Ottawa Tools dialog's
/// "window never touches the model, Command.cs always transacts" split —
/// this is a multi-action workbench you keep open across several Apply
/// clicks, not a single one-shot dialog).
///
/// Create+Bind and Jammer live in their own partial-class files
/// (ParamPowerSuiteEngine.CreateParam.cs / .Jammer.cs) since they touch
/// meaningfully different parts of the API (shared parameter files, family
/// documents) — kept apart rather than crammed into one huge file.
/// </summary>
public static partial class ParamPowerSuiteEngine
{
    public static List<Element> LoadElements(Document doc, IReadOnlyCollection<BuiltInCategory> categories, ElementId? levelId)
    {
        if (categories.Count == 0) return new List<Element>();

        var filters = categories.Select(c => (ElementFilter)new ElementCategoryFilter(c)).ToList();
        var filter = filters.Count == 1 ? filters[0] : new LogicalOrFilter(filters);

        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .WherePasses(filter)
            .ToList();

        if (levelId is not null)
            elements = elements.Where(e => e.LevelId == levelId).ToList();

        return elements;
    }

    /// <summary>Groups loaded elements by their type's (FamilyName, Name) — this works uniformly
    /// across both loadable-family categories (Doors, Furniture, …) and system-family categories
    /// (Walls, Floors, …), since every element's ElementType carries FamilyName/Name either way.</summary>
    public static List<FamilyTypeGroup> GroupByFamilyType(List<Element> elements)
    {
        return elements
            .Select(e => e.Document.GetElement(e.GetTypeId()) as ElementType)
            .Where(t => t is not null)
            .GroupBy(t => (t!.FamilyName, t.Name))
            .Select(g => new FamilyTypeGroup(g.Key.FamilyName, g.Key.Name, g.Count()))
            .OrderBy(g => g.FamilyName).ThenBy(g => g.TypeName)
            .ToList();
    }

    public static string ElementLabel(Element element)
    {
        var type = element.Document.GetElement(element.GetTypeId()) as ElementType;
        return type is null ? $"{element.Category?.Name ?? "Element"} #{element.Id}" : $"{type.FamilyName} : {type.Name} (#{element.Id})";
    }

    // ---------- Tab 1: Bulk Set ----------

    /// <summary>
    /// The 5 fixed Identity Data fields Bulk Set's UI offers, mapped to their real
    /// BuiltInParameter — confirmed live (user-reported): element.LookupParameter("Comments")
    /// matches by the parameter's CURRENT DISPLAY name, which is locale-dependent (on a
    /// German-language Revit install "Comments" displays as "Kommentare"), so the literal
    /// English string this dictionary used to pass straight into LookupParameter matched
    /// nothing at all on that install — every element came back Skipped, 0 Applied, every
    /// single time, regardless of what was typed into the field. BuiltInParameter is a stable
    /// enum id, not a display string, so it works the same on every language Revit runs in.
    /// </summary>
    private static readonly Dictionary<string, BuiltInParameter> BulkSetFields = new()
    {
        ["Comments"] = BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS,
        ["Mark"] = BuiltInParameter.ALL_MODEL_MARK,
        ["Type Comments"] = BuiltInParameter.ALL_MODEL_TYPE_COMMENTS,
        ["Keynote"] = BuiltInParameter.KEYNOTE_PARAM,
        ["Description"] = BuiltInParameter.ALL_MODEL_DESCRIPTION,
    };

    /// <summary>Writes every non-empty (field, value) pair onto every element that has a
    /// writable parameter for that field. Counts at the (element × field) level, not per
    /// element, so the status bar reflects how much work actually happened across a
    /// multi-field bulk set.</summary>
    public static ParamOpResult ApplyBulkSet(List<Element> elements, IReadOnlyDictionary<string, string> values)
    {
        var filled = values.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToList();
        if (filled.Count == 0) return new ParamOpResult(0, 0, 0);

        int applied = 0, skipped = 0, failed = 0;
        foreach (var element in elements)
        foreach (var (name, value) in filled)
        {
            if (!BulkSetFields.TryGetValue(name, out var bip))
            {
                skipped++;
                continue;
            }

            var param = ResolveIdentityParameter(element, bip);
            if (param is not { IsReadOnly: false })
            {
                skipped++;
                continue;
            }

            try { if (SetParameterValue(param, value)) applied++; else failed++; }
            catch (Exception) { failed++; }
        }

        return new ParamOpResult(applied, skipped, failed);
    }

    /// <summary>These Identity Data fields aren't consistently instance- or type-level across
    /// every category — Comments/Mark are instance-owned, Type Comments is type-owned, and
    /// Description varies by category — so this checks the instance first and only falls back
    /// to the element's own type if the built-in parameter isn't present there.</summary>
    private static Parameter? ResolveIdentityParameter(Element element, BuiltInParameter bip)
    {
        var instanceParam = element.get_Parameter(bip);
        if (instanceParam is not null) return instanceParam;

        var type = element.Document.GetElement(element.GetTypeId());
        return type?.get_Parameter(bip);
    }

    private static bool SetParameterValue(Parameter param, string value) => param.StorageType switch
    {
        StorageType.String => param.Set(value),
        StorageType.Integer => int.TryParse(value, out var i) && param.Set(i),
        StorageType.Double => param.SetValueString(value),
        _ => false,
    };

    // ---------- Tab 2: Find / Replace ----------

    public static List<ParamChangePreview> PreviewFindReplace(List<Element> elements, string paramName, string find, string replace, bool useRegex, bool caseSensitive)
        => Preview(elements, paramName, oldValue => ApplyFindReplace(oldValue, find, replace, useRegex, caseSensitive));

    public static string ApplyFindReplace(string input, string find, string replace, bool useRegex, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(find)) return input;

        if (useRegex)
        {
            try
            {
                var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                return Regex.Replace(input, find, replace, options);
            }
            catch (ArgumentException)
            {
                return input; // invalid pattern — leave the value alone rather than throw mid-preview
            }
        }

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return input.Replace(find, replace, comparison);
    }

    // ---------- Tab 3: Case Transform ----------

    public static List<ParamChangePreview> PreviewCaseTransform(List<Element> elements, string paramName, CaseMode mode)
        => Preview(elements, paramName, oldValue => ApplyCaseTransform(oldValue, mode));

    public static string ApplyCaseTransform(string input, CaseMode mode) => mode switch
    {
        CaseMode.Upper => input.ToUpperInvariant(),
        CaseMode.Lower => input.ToLowerInvariant(),
        CaseMode.Title => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ToLowerInvariant()),
        _ => input,
    };

    // ---------- Tab 4: Copy A -> B ----------

    public static List<ParamChangePreview> PreviewCopy(List<Element> elements, string sourceParam, string targetParam, CopyMode mode)
    {
        var results = new List<ParamChangePreview>();
        foreach (var element in elements)
        {
            var source = element.LookupParameter(sourceParam);
            var target = element.LookupParameter(targetParam);
            if (source is null || target is not { IsReadOnly: false }) continue;

            var sourceValue = ReadValue(source);
            var targetValue = ReadValue(target);
            var newValue = mode == CopyMode.Overwrite ? sourceValue : $"{targetValue}{sourceValue}";
            if (newValue == targetValue) continue;

            results.Add(new ParamChangePreview(element.Id, ElementLabel(element), targetValue, newValue));
        }
        return results;
    }

    // ---------- Tab 5: Combine (Tokens) ----------

    private static readonly Regex TokenPattern = new(@"\{([^{}]+)\}", RegexOptions.Compiled);

    public static List<ParamChangePreview> PreviewCombine(List<Element> elements, string template, string targetParam)
    {
        var results = new List<ParamChangePreview>();
        foreach (var element in elements)
        {
            var target = element.LookupParameter(targetParam);
            if (target is not { IsReadOnly: false }) continue;

            var oldValue = ReadValue(target);
            var newValue = ResolveCombineTemplate(template, element);
            if (newValue == oldValue) continue;

            results.Add(new ParamChangePreview(element.Id, ElementLabel(element), oldValue, newValue));
        }
        return results;
    }

    /// <summary>Resolves {Type Name}, {Family Name}, {Level}, and {AnyParameterName} tokens against
    /// one element — an unmatched or unrecognized token resolves to an empty string rather than
    /// throwing, so a typo in the template shows up as a visibly missing chunk in Preview, not a crash.</summary>
    public static string ResolveCombineTemplate(string template, Element element)
        => TokenPattern.Replace(template, m => ResolveToken(element, m.Groups[1].Value.Trim()));

    private static string ResolveToken(Element element, string token)
    {
        var type = element.Document.GetElement(element.GetTypeId()) as ElementType;
        if (string.Equals(token, "Type Name", StringComparison.OrdinalIgnoreCase)) return type?.Name ?? "";
        if (string.Equals(token, "Family Name", StringComparison.OrdinalIgnoreCase)) return type?.FamilyName ?? "";
        if (string.Equals(token, "Level", StringComparison.OrdinalIgnoreCase))
            return element.LevelId == ElementId.InvalidElementId ? "" : element.Document.GetElement(element.LevelId)?.Name ?? "";

        var param = element.LookupParameter(token);
        return param is null ? "" : ReadValue(param);
    }

    // ---------- shared preview/apply plumbing ----------

    private static List<ParamChangePreview> Preview(List<Element> elements, string paramName, Func<string, string> transform)
    {
        var results = new List<ParamChangePreview>();
        foreach (var element in elements)
        {
            var param = element.LookupParameter(paramName);
            if (param is not { StorageType: StorageType.String, IsReadOnly: false }) continue;

            var oldValue = param.AsString() ?? "";
            var newValue = transform(oldValue);
            if (newValue == oldValue) continue;

            results.Add(new ParamChangePreview(element.Id, ElementLabel(element), oldValue, newValue));
        }
        return results;
    }

    private static string ReadValue(Parameter param) => param.AsValueString() ?? param.AsString() ?? "";

    public record ApplyFailure(string ElementLabel, string Reason);

    /// <summary>Writes a previously computed preview back onto its target parameter — shared by
    /// Find/Replace, Case Transform, Copy A→B, and Combine. Re-reads each element by Id rather than
    /// trusting the Element references captured during Preview, since Preview and Apply always run
    /// moments apart in the same session here, but keeping them decoupled costs nothing and avoids
    /// ever writing through a stale reference.</summary>
    public static (ParamOpResult Result, List<ApplyFailure> Failures) ApplyChanges(Document doc, List<ParamChangePreview> changes, string targetParamName)
    {
        int applied = 0, skipped = 0, failed = 0;
        var failures = new List<ApplyFailure>();

        foreach (var change in changes)
        {
            var element = doc.GetElement(change.ElementId);
            var param = element?.LookupParameter(targetParamName);
            if (param is not { IsReadOnly: false }) { skipped++; continue; }

            try
            {
                // SetValueString parses a UI-formatted string into whatever the
                // parameter's real storage type is (units for Double, an element
                // lookup by name for ElementId, etc.) — exactly what a
                // Double/Integer/ElementId target needs (Copy A→B and Combine
                // can target either). A String parameter has no such formatting
                // step, and confirmed live (user-reported): running free text
                // through that parsing path rejected every single row on a
                // String-storage target (Find/Replace and Case Transform are
                // always String — see Preview's own StorageType.String guard —
                // so this was breaking both of them outright). Parameter.Set
                // writes a String value directly, no formatting/parsing
                // involved — same fix already proven in BatchExcelSyncEngine.Commit.
                var success = param.StorageType == StorageType.String
                    ? param.Set(change.NewValue)
                    : param.SetValueString(change.NewValue);

                if (success)
                {
                    applied++;
                }
                else
                {
                    failed++;
                    failures.Add(new ApplyFailure(change.ElementLabel, "Revit rejected the value — it doesn't match this parameter's expected format"));
                }
            }
            catch (Exception ex)
            {
                failed++;
                failures.Add(new ApplyFailure(change.ElementLabel, ex.Message));
            }
        }

        return (new ParamOpResult(applied, skipped, failed), failures);
    }
}
