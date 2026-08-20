using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

/// <summary>
/// Rewrites entity class keywords directly in a freshly-exported .ifc
/// (STEP text) file, bypassing Revit's own exporter entirely. Confirmed
/// against a real project: setting the built-in "Export Type to IFC As"
/// parameter — Revit's own documented mechanism for this, and the only
/// one that currently exists (see IfcClassMapper) — had zero effect on
/// the export even when set by hand directly in Revit's UI, with no
/// plugin involved at all. Whatever's blocking that (a project export
/// setup override, a version-specific Autodesk regression, or something
/// else) can't be fixed from here, so this sidesteps it: after Revit
/// writes the file, find each matched element by its IfcGUID (the same
/// value Revit writes as the entity's GlobalId) and swap just the class
/// keyword on that line, leaving every argument untouched.
///
/// Safe as long as the old and new classes share the same attribute
/// count — true for every class in IfcClassMapper.DefaultRules, all
/// simple IfcElement-based occurrence entities with a single trailing
/// (and, in practice, unset/"$") PredefinedType attribute.
/// </summary>
public static class IfcFilePostProcessor
{
    private static readonly Regex EntityLine = new(@"^(#\d+\s*=\s*)IFC[A-Z0-9]+(\('([^']+)'.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Maps each matched element's IfcGUID to its target IFC class (upper
    /// case, e.g. "IFCLIGHTFIXTURE"), scanning every instance of every
    /// matched, ownable type — not the types themselves, since the
    /// exported STEP entities are per-instance, not per-type.
    /// </summary>
    public static Dictionary<string, string> BuildGuidToClassMap(
        Document doc,
        Dictionary<IfcClassMapper.Rule, List<ElementType>> matchesByRule,
        ISet<ElementId> notOwnedTypes)
    {
        var map = new Dictionary<string, string>();
        foreach (var (rule, types) in matchesByRule)
        {
            var typeIds = types.Where(t => !notOwnedTypes.Contains(t.Id)).Select(t => t.Id).ToHashSet();
            if (typeIds.Count == 0) continue;

            var instances = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Where(e => typeIds.Contains(e.GetTypeId()));

            foreach (var instance in instances)
            {
                var guid = instance.get_Parameter(BuiltInParameter.IFC_GUID)?.AsString();
                if (string.IsNullOrEmpty(guid)) continue;
                map[guid] = rule.IfcClass.ToUpperInvariant();
            }
        }
        return map;
    }

    /// <summary>
    /// Rewrites matching entity lines in place. Returns how many lines
    /// were changed, so a mapped GUID that never actually shows up in the
    /// exported file (e.g. an instance Revit excluded from export
    /// entirely) is visible as a shortfall instead of a silent no-op.
    /// </summary>
    public static int RewriteClasses(string ifcFilePath, Dictionary<string, string> guidToClass)
    {
        if (guidToClass.Count == 0) return 0;

        var lines = System.IO.File.ReadAllLines(ifcFilePath);
        var changed = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var match = EntityLine.Match(lines[i]);
            if (!match.Success) continue;

            var guid = match.Groups[3].Value;
            if (!guidToClass.TryGetValue(guid, out var newClass)) continue;

            lines[i] = match.Groups[1].Value + newClass + match.Groups[2].Value;
            changed++;
        }

        if (changed > 0) System.IO.File.WriteAllLines(ifcFilePath, lines);
        return changed;
    }
}
