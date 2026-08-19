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

    public record ApplyResult(int Updated, int SkippedNotOwned);

    /// <summary>
    /// Applies each rule to every element type whose family or type name
    /// contains the rule's text (case-insensitive), setting IfcExportAs on
    /// that type. Must run inside an open transaction (same one as
    /// <see cref="EnsureParameterExists"/> is fine).
    ///
    /// In a workshared (central) model, a matched type can be owned by
    /// another user's session — confirmed against a real project, where
    /// several switch/outlet types were rejected mid-transaction with
    /// Revit's own "no permission to edit" failure dialog. Proactively
    /// requesting ownership via WorksharingUtils.CheckoutElements first
    /// turns that into a clean per-rule skip count instead of aborting the
    /// whole operation, and lets every type that IS available still get
    /// updated in the same run.
    /// </summary>
    public static Dictionary<Rule, ApplyResult> Apply(Document doc, IEnumerable<Rule> rules)
    {
        var types = new FilteredElementCollector(doc)
            .WhereElementIsElementType()
            .OfType<ElementType>()
            .ToList();

        var rulesList = rules.ToList();
        var matchesByRule = rulesList.ToDictionary(rule => rule, rule => types.Where(t =>
            t.Name.Contains(rule.NameContains, StringComparison.OrdinalIgnoreCase) ||
            t.FamilyName.Contains(rule.NameContains, StringComparison.OrdinalIgnoreCase)).ToList());

        var notOwned = new HashSet<ElementId>();
        if (doc.IsWorkshared)
        {
            var allMatchedIds = matchesByRule.Values.SelectMany(list => list.Select(t => t.Id)).Distinct().ToList();
            if (allMatchedIds.Count > 0)
                notOwned = new HashSet<ElementId>(WorksharingUtils.CheckoutElements(doc, allMatchedIds));
        }

        var results = new Dictionary<Rule, ApplyResult>();
        foreach (var rule in rulesList)
        {
            var updated = 0;
            var skipped = 0;
            foreach (var type in matchesByRule[rule])
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
