using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

/// <summary>
/// Overrides Revit's default category-to-IFC-class mapping per family/type
/// via the "IfcExportAs" shared parameter Revit's own IFC exporter already
/// reads — the standard, intended way to do this (not a workaround), since
/// the exporter's category-level default can't tell two families in the
/// same category apart (e.g. a smoke alarm and a light switch both filed
/// under Specialty Equipment need different IFC classes).
/// </summary>
public static class IfcClassMapper
{
    public const string ParameterName = "IfcExportAs";

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
    /// Ensures the "IfcExportAs" shared parameter is bound at the type
    /// level across every model category, so it can be set once per
    /// family type and inherited by every instance. Must run inside an
    /// open transaction. Safe to call repeatedly — Insert/ReInsert are
    /// no-ops (or a scope widening) if the binding already exists.
    /// </summary>
    public static void EnsureParameterExists(Autodesk.Revit.ApplicationServices.Application app, Document doc)
    {
        var sharedParamPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BIMFlow_SharedParameters.txt");
        if (!System.IO.File.Exists(sharedParamPath))
            System.IO.File.WriteAllText(sharedParamPath, string.Empty);

        // OpenSharedParameterFile() reads whatever file SharedParametersFilename
        // currently points at — switching it to our own file just for this call
        // would clobber the user's own shared parameter file setting for the
        // rest of the Revit session, so restore it afterward either way.
        var previousFilename = app.SharedParametersFilename;
        try
        {
            app.SharedParametersFilename = sharedParamPath;
            var defFile = app.OpenSharedParameterFile()
                ?? throw new InvalidOperationException("Could not open or create the shared parameter file.");

            var group = defFile.Groups.get_Item("BIMFlow") ?? defFile.Groups.Create("BIMFlow");
            var definition = group.Definitions.get_Item(ParameterName)
                ?? group.Definitions.Create(new ExternalDefinitionCreationOptions(ParameterName, SpecTypeId.String.Text));

            var categorySet = app.Create.NewCategorySet();
            foreach (Category category in doc.Settings.Categories)
            {
                if (category.CategoryType == CategoryType.Model && category.AllowsBoundParameters)
                    categorySet.Insert(category);
            }

            var binding = app.Create.NewTypeBinding(categorySet);
            var bindings = doc.ParameterBindings;
            if (!bindings.Insert(definition, binding, GroupTypeId.Ifc))
                bindings.ReInsert(definition, binding, GroupTypeId.Ifc);
        }
        finally
        {
            app.SharedParametersFilename = previousFilename;
        }
    }

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
    /// Sets IfcExportAs on every matched type not in <paramref
    /// name="notOwned"/>. Must run inside an open transaction (same one as
    /// <see cref="EnsureParameterExists"/> is fine).
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

                var p = type.LookupParameter(ParameterName);
                if (p is not { IsReadOnly: false }) continue;
                p.Set(rule.IfcClass);
                updated++;
            }
            results[rule] = new ApplyResult(updated, skipped);
        }
        return results;
    }
}
