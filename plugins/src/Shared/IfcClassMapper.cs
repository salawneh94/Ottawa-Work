using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

/// <summary>
/// Overrides Revit's default category-to-IFC-class mapping per family/type,
/// since the exporter's category-level default can't tell two families in
/// the same category apart (e.g. a smoke alarm and a light switch both
/// filed under Specialty Equipment need different IFC classes).
///
/// Sets the built-in "Export Type to IFC As" parameter
/// (BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE_AS) — NOT a custom shared
/// parameter named "IfcExportAs". That older shared-parameter mechanism
/// (still what most blog posts and forum answers describe) was replaced by
/// this built-in parameter starting Revit 2023.1
/// (github.com/Autodesk/revit-ifc issue #614) — confirmed against a real
/// project, where a custom "IfcExportAs" shared parameter set successfully
/// on every matched type had zero effect on a fresh export. The built-in
/// parameter already exists on every type by default, so unlike the old
/// approach this needs no shared-parameter-file creation or project-wide
/// binding step at all — setting it is an ordinary type parameter edit.
/// </summary>
public static class IfcClassMapper
{
    public record Rule(string NameContains, string IfcClass);

    /// <summary>Seeded from a real client mapping table handed over in a coordination meeting.</summary>
    public static readonly Rule[] DefaultRules =
    {
        new("Steckdose", "IfcOutlet"),
        new("Leuchte", "IfcLightFixture"),
        new("Türkontakt", "IfcSensor"),
        new("Temperatursensor", "IfcSensor"),
        new("Taster", "IfcSwitchingDevice"),
        new("Lichtschalter", "IfcSwitchingDevice"),
        new("Lüftungsauslass", "IfcAirTerminal"),
        new("Lüftungsgitter", "IfcAirTerminal"),
        new("Sirene", "IfcAlarm"),
        new("Alarmgeber", "IfcAlarm"),
        new("Feuerlöscher", "IfcFireSuppressionTerminal"),
        new("Lüftungsklappe", "IfcDamper"),
        new("Ventilator", "IfcFan"),
    };

    /// <summary>
    /// Finds every element type matching each rule's name text, without
    /// modifying anything. Deliberately separate from <see cref="Apply"/> —
    /// call this and <see cref="RequestOwnership"/> BEFORE starting a
    /// transaction; WorksharingUtils.CheckoutElements is a round-trip
    /// against the central model's ownership bookkeeping, not a local
    /// document edit, and calling it from inside an already-open
    /// transaction produced consistently wrong results when tested against
    /// a real project (every matched type reported as owned by another
    /// user, even on a file detached from central with no other session
    /// open at all).
    /// </summary>
    public static Dictionary<Rule, List<ElementType>> Match(Document doc, IEnumerable<Rule> rules)
    {
        var types = new FilteredElementCollector(doc)
            .WhereElementIsElementType()
            .OfType<ElementType>()
            .ToList();

        return rules.ToDictionary(rule => rule, rule => types.Where(t =>
            t.Name.Contains(rule.NameContains, StringComparison.OrdinalIgnoreCase) ||
            t.FamilyName.Contains(rule.NameContains, StringComparison.OrdinalIgnoreCase)).ToList());
    }

    /// <summary>
    /// Requests ownership of every matched type up front, in a workshared
    /// model, before any transaction is open — see <see cref="Match"/> for
    /// why this must run outside a transaction. Returns the ids that
    /// couldn't be checked out (owned by someone else). Best-effort: any
    /// failure from the API itself (e.g. no live central connection) is
    /// swallowed and treated as "nothing blocked," since Apply()'s own
    /// per-type Set() is what actually determines success either way.
    /// </summary>
    public static HashSet<ElementId> RequestOwnership(Document doc, Dictionary<Rule, List<ElementType>> matchesByRule)
    {
        if (!doc.IsWorkshared) return new HashSet<ElementId>();

        var allIds = matchesByRule.Values.SelectMany(list => list.Select(t => t.Id)).Distinct().ToList();
        if (allIds.Count == 0) return new HashSet<ElementId>();

        try
        {
            return new HashSet<ElementId>(WorksharingUtils.CheckoutElements(doc, allIds));
        }
        catch
        {
            return new HashSet<ElementId>();
        }
    }

    public record ApplyResult(int Updated, int SkippedNotOwned);

    /// <summary>
    /// Sets "Export Type to IFC As" on every matched type not in <paramref
    /// name="notOwned"/>. Must run inside an open transaction.
    /// </summary>
    public static Dictionary<Rule, ApplyResult> Apply(Dictionary<Rule, List<ElementType>> matchesByRule, ISet<ElementId> notOwned)
    {
        var results = new Dictionary<Rule, ApplyResult>();
        foreach (var (rule, matches) in matchesByRule)
        {
            var updated = 0;
            var skipped = 0;
            foreach (var type in matches)
            {
                if (notOwned.Contains(type.Id)) { skipped++; continue; }

                var p = type.get_Parameter(BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE_AS);
                if (p is not { IsReadOnly: false, StorageType: StorageType.String }) continue;
                p.Set(rule.IfcClass);
                updated++;
            }
            results[rule] = new ApplyResult(updated, skipped);
        }
        return results;
    }
}
