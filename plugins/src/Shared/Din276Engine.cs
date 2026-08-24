using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace OttawaWork.Shared;

public enum QuantityUnit { SquareMeters, CubicMeters, Meters, Count }

/// <summary>One DIN 276 (2nd-level, 3-digit) Kostengruppe this tool knows how to price.</summary>
public record KostengruppeDef(string Code, string Name, QuantityUnit Unit);

public record ElementQuantity(ElementId ElementId, string Code, double Quantity);

public record KostengruppeTotal(string Code, string Name, QuantityUnit Unit, double Quantity, double Rate, double Subtotal);

/// <summary>
/// Classifies model elements into DIN 276 Kostengruppen and prices them
/// against user-supplied unit rates — this tool deliberately ships with no
/// built-in €/unit numbers, since real construction rates vary by region
/// and year far too much to hardcode responsibly; the rate table starts at
/// zero and the user fills it in (or imports one they already have).
///
/// Classification is a two-tier lookup: an explicit "Kostengruppe" project/
/// shared parameter on the element wins if present (e.g. "331", as text or
/// as a number — both storage types are read), letting a firm override or
/// extend the defaults per-project without touching this code, to ANY code
/// (see GetOrSynthesizeDef — an override code doesn't have to be one this
/// tool ships a definition for); otherwise a small built-in rule table maps
/// common categories to their DIN 276 group. Walls resolve to a real
/// 3rd-level code (331/341 tragend, 332/342 nichttragend, exterior/interior
/// respectively) off Revit's own Wall.StructuralUsage, not a guess — see
/// WallCode. Doors/windows get their own 334/344 (a door is never itself
/// "tragend", independent of its host wall's structural role), following
/// whichever host wall's exterior/interior split they're hosted in. This
/// only covers the categories with an unambiguous DIN 276 home — Columns/
/// Framing and most nutzungsspezifische equipment are deliberately left
/// unmapped rather than guessed, and simply won't appear in the report
/// unless given an explicit Kostengruppe parameter value.
///
/// EnsureKostengruppeParameter creates and binds that "Kostengruppe"
/// parameter itself if the project doesn't already have one, so "Assign to
/// elements" never has to skip elements just because nobody set the
/// parameter up yet. TryAssignKostengruppe then writes the resolved code
/// onto it (falling back to Assembly Code / Type Comments only if binding
/// genuinely didn't cover an element — see its own doc comment). Command.cs
/// wraps both calls in a single transaction with rollback — the only part
/// of this tool that touches the model.
///
/// Quantities read Revit's native area/volume/length parameters, which are
/// always stored in internal (feet-based) units regardless of the
/// project's display units — UnitUtils.ConvertFromInternalUnits is used
/// everywhere so the numbers that reach the report are real square
/// meters/cubic meters/meters, not feet mislabeled as metric.
/// </summary>
public static class Din276Engine
{
    public const string ParameterOverrideName = "Kostengruppe";
    private const string SharedParameterGroupName = "Ottawa Tools";

    /// <summary>
    /// 2nd-level groups plus the well-established 3rd-level (Kostenuntergruppen)
    /// breakdown under 330/340/350/360 — the categories this tool already
    /// classifies elements into, so a real breakdown was worth the extra
    /// table rows. 400-series 3rd-level codes aren't listed here (the exact
    /// official subdivision text is less certain from memory than the
    /// widely-published 330/340/350/360 breakdown, and getting a cost-code
    /// NAME wrong in an actual accounting tool is worse than not offering
    /// one) — but see GetOrSynthesizeDef below: an unlisted code a firm
    /// types into the "Kostengruppe" override parameter (a 400-series
    /// sub-code, or anything else) is never silently dropped just because
    /// it isn't a row here.
    /// </summary>
    public static readonly KostengruppeDef[] Kostengruppen =
    {
        new("310", "Baugrube", QuantityUnit.SquareMeters),
        new("320", "Gründung", QuantityUnit.SquareMeters),

        new("330", "Außenwände", QuantityUnit.SquareMeters),
        new("331", "Tragende Außenwände", QuantityUnit.SquareMeters),
        new("332", "Nichttragende Außenwände", QuantityUnit.SquareMeters),
        new("333", "Außenstützen", QuantityUnit.SquareMeters),
        new("334", "Außentüren und -fenster", QuantityUnit.SquareMeters),
        new("335", "Außenwandbekleidungen, außen", QuantityUnit.SquareMeters),
        new("336", "Außenwandbekleidungen, innen", QuantityUnit.SquareMeters),
        new("337", "Elementierte Außenwände", QuantityUnit.SquareMeters),
        new("338", "Sonnenschutz", QuantityUnit.SquareMeters),
        new("339", "Außenwände, sonstiges", QuantityUnit.SquareMeters),

        new("340", "Innenwände", QuantityUnit.SquareMeters),
        new("341", "Tragende Innenwände", QuantityUnit.SquareMeters),
        new("342", "Nichttragende Innenwände", QuantityUnit.SquareMeters),
        new("343", "Innenstützen", QuantityUnit.SquareMeters),
        new("344", "Innentüren und -fenster", QuantityUnit.SquareMeters),
        new("345", "Innenwandbekleidungen", QuantityUnit.SquareMeters),
        new("346", "Elementierte Innenwände", QuantityUnit.SquareMeters),
        new("349", "Innenwände, sonstiges", QuantityUnit.SquareMeters),

        new("350", "Decken", QuantityUnit.SquareMeters),
        new("351", "Deckenkonstruktionen", QuantityUnit.SquareMeters),
        new("352", "Deckenbeläge", QuantityUnit.SquareMeters),
        new("353", "Deckenbekleidungen", QuantityUnit.SquareMeters),
        new("359", "Decken, sonstiges", QuantityUnit.SquareMeters),

        new("360", "Dächer", QuantityUnit.SquareMeters),
        new("361", "Dachkonstruktionen", QuantityUnit.SquareMeters),
        new("362", "Dachfenster, Dachöffnungen", QuantityUnit.SquareMeters),
        new("363", "Dachbeläge", QuantityUnit.SquareMeters),
        new("364", "Dachbekleidungen", QuantityUnit.SquareMeters),
        new("369", "Dächer, sonstiges", QuantityUnit.SquareMeters),

        new("370", "Baukonstruktive Einbauten", QuantityUnit.Count),
        new("390", "Sonstige Maßnahmen für Baukonstruktionen", QuantityUnit.Count),

        new("410", "Abwasser-, Wasser-, Gasanlagen", QuantityUnit.Meters),
        new("420", "Wärmeversorgungsanlagen", QuantityUnit.Count),
        new("430", "Raumlufttechnische Anlagen", QuantityUnit.Meters),
        new("440", "Elektrische Anlagen", QuantityUnit.Count),
        new("450", "Kommunikations- und Sicherheitstechnik", QuantityUnit.Count),
        new("460", "Förderanlagen", QuantityUnit.Count),
        new("470", "Nutzungsspezifische Anlagen und Ausstattung", QuantityUnit.Count),
        new("480", "Gebäude- und Anlagenautomation", QuantityUnit.Count),
        new("490", "Sonstige Maßnahmen für technische Anlagen", QuantityUnit.Count),
    };

    private static readonly (BuiltInCategory Category, string Code)[] DefaultCategoryRules =
    {
        (BuiltInCategory.OST_Floors, "350"),
        (BuiltInCategory.OST_Roofs, "360"),
        (BuiltInCategory.OST_PlumbingFixtures, "410"),
        (BuiltInCategory.OST_PipeCurves, "410"),
        (BuiltInCategory.OST_MechanicalEquipment, "420"),
        (BuiltInCategory.OST_DuctCurves, "430"),
        (BuiltInCategory.OST_ElectricalEquipment, "440"),
        (BuiltInCategory.OST_ElectricalFixtures, "440"),
        (BuiltInCategory.OST_LightingFixtures, "440"),
        (BuiltInCategory.OST_CommunicationDevices, "450"),
        (BuiltInCategory.OST_FireAlarmDevices, "450"),
        (BuiltInCategory.OST_SecurityDevices, "450"),
    };

    /// <summary>Every category the "Kostengruppe" project parameter gets bound to when
    /// EnsureKostengruppeParameter creates it — every category Classify/ResolveCode ever
    /// looks at, plus Structural Columns/Framing and Ceilings, which have no default rule
    /// (see DefaultCategoryRules) but can still carry an explicit override value.</summary>
    private static readonly BuiltInCategory[] BindableCategories =
    {
        BuiltInCategory.OST_Walls,
        BuiltInCategory.OST_Doors,
        BuiltInCategory.OST_Windows,
        BuiltInCategory.OST_Floors,
        BuiltInCategory.OST_Ceilings,
        BuiltInCategory.OST_Roofs,
        BuiltInCategory.OST_StructuralColumns,
        BuiltInCategory.OST_StructuralFraming,
        BuiltInCategory.OST_PlumbingFixtures,
        BuiltInCategory.OST_PipeCurves,
        BuiltInCategory.OST_MechanicalEquipment,
        BuiltInCategory.OST_DuctCurves,
        BuiltInCategory.OST_ElectricalEquipment,
        BuiltInCategory.OST_ElectricalFixtures,
        BuiltInCategory.OST_LightingFixtures,
        BuiltInCategory.OST_CommunicationDevices,
        BuiltInCategory.OST_FireAlarmDevices,
        BuiltInCategory.OST_SecurityDevices,
    };

    public static List<ElementQuantity> Classify(List<Element> elements)
    {
        var categoryCodes = DefaultCategoryRules.ToDictionary(r => r.Category, r => r.Code);
        var results = new List<ElementQuantity>();

        foreach (var element in elements)
        {
            var code = ResolveCode(element, categoryCodes);
            if (code is null) continue;

            var def = GetOrSynthesizeDef(code);
            results.Add(new ElementQuantity(element.Id, code, GetQuantity(element, def.Unit)));
        }

        return results;
    }

    public static List<KostengruppeTotal> Aggregate(List<ElementQuantity> quantities, IReadOnlyDictionary<string, double> rates)
    {
        var totals = new List<KostengruppeTotal>();
        // Every code actually seen, not just the ones this table happens to
        // list — a firm's own "Kostengruppe" override value always wins in
        // ResolveCode regardless of whether it's one of ours, so a code we
        // don't recognize (a 400-series sub-code, or a firm-specific one)
        // still needs a report line instead of silently vanishing.
        foreach (var code in quantities.Select(q => q.Code).Distinct().OrderBy(c => c, StringComparer.Ordinal))
        {
            var def = GetOrSynthesizeDef(code);
            var quantity = quantities.Where(q => q.Code == code).Sum(q => q.Quantity);
            var rate = rates.TryGetValue(code, out var r) ? r : 0;
            totals.Add(new KostengruppeTotal(code, def.Name, def.Unit, quantity, rate, quantity * rate));
        }
        return totals;
    }

    /// <summary>Looks up a code in the built-in table; if it's not there (a firm-specific or
    /// otherwise unlisted code typed into the override parameter), falls back to its 2nd-level
    /// parent's unit (e.g. "371" inherits "370"'s unit) so quantities are still measured
    /// sensibly, defaulting to Count only if even that parent is unknown.</summary>
    private static KostengruppeDef GetOrSynthesizeDef(string code)
    {
        var exact = Kostengruppen.FirstOrDefault(k => k.Code == code);
        if (exact is not null) return exact;

        if (code.Length == 3 && char.IsDigit(code[0]) && char.IsDigit(code[1]))
        {
            var parentCode = $"{code[0]}{code[1]}0";
            var parent = Kostengruppen.FirstOrDefault(k => k.Code == parentCode);
            if (parent is not null) return new KostengruppeDef(code, $"Kostengruppe {code}", parent.Unit);
        }

        return new KostengruppeDef(code, $"Kostengruppe {code}", QuantityUnit.Count);
    }

    private static string? ResolveCode(Element element, Dictionary<BuiltInCategory, string> categoryCodes)
    {
        var overrideValue = ReadOverrideParameter(element);
        if (!string.IsNullOrWhiteSpace(overrideValue)) return overrideValue;

        if (element is Wall wall)
            return WallCode(wall);

        var category = element.Category;
        if (category is not null && (category.Id == new ElementId(BuiltInCategory.OST_Doors) || category.Id == new ElementId(BuiltInCategory.OST_Windows)))
        {
            // Doors/windows get their own 3rd-level code (334/344) directly —
            // "Außentüren und -fenster" / "Innentüren und -fenster" is what DIN
            // 276 actually calls this, independent of whether the host wall
            // happens to be load-bearing (a door is never itself "tragend").
            var hostWall = (element as FamilyInstance)?.Host as Wall;
            return hostWall?.WallType?.Function == WallFunction.Interior ? "344" : "334";
        }

        if (category is null) return null;
        foreach (var (builtIn, code) in categoryCodes)
            if (category.Id == new ElementId(builtIn)) return code;

        return null;
    }

    /// <summary>
    /// Splits a wall into its real 3rd-level DIN 276 code using Revit's own
    /// Wall.StructuralUsage — Bearing/Shear/Combined means "tragend" (331
    /// exterior / 341 interior), NonBearing means "nichttragend" (332 / 342).
    /// A wall not marked Structural at all in Revit reports NonBearing here
    /// too, which is the correct DIN 276 answer regardless: a wall nobody
    /// told Revit carries load isn't "tragend" by definition. This replaces
    /// guessing — the previous version left every wall at the bare 330/340
    /// level and made the user manually tag which ones were load-bearing.
    /// </summary>
    private static string WallCode(Wall wall)
    {
        var isInterior = wall.WallType?.Function == WallFunction.Interior;
        var isBearing = wall.StructuralUsage is StructuralWallUsage.Bearing or StructuralWallUsage.Shear or StructuralWallUsage.Combined;

        return (isInterior, isBearing) switch
        {
            (false, true) => "331",
            (false, false) => "332",
            (true, true) => "341",
            (true, false) => "342",
        };
    }

    /// <summary>
    /// Reads the "Kostengruppe" override parameter regardless of whether the
    /// firm set it up as Text or as a Number — DIN 276 codes are 3-digit
    /// numeric strings, and it's equally reasonable for a project parameter
    /// to store "330" as an integer as it is to store it as text, so only
    /// checking StorageType.String (an earlier version's bug) silently
    /// ignored every element on a project that chose the numeric route.
    /// </summary>
    private static string? ReadOverrideParameter(Element element)
    {
        var param = element.LookupParameter(ParameterOverrideName);
        if (param is not { HasValue: true }) return null;

        return param.StorageType switch
        {
            StorageType.String => param.AsString(),
            StorageType.Integer => param.AsInteger().ToString(),
            _ => null,
        };
    }

    /// <summary>
    /// Writes a resolved Kostengruppe code back onto the element's own
    /// "Kostengruppe" parameter. Command.cs calls EnsureKostengruppeParameter
    /// first, so in the normal case that parameter now exists and is bound
    /// for every category this tool handles — this just writes to it.
    ///
    /// The only elements that fall through to a builtin parameter are ones
    /// whose category somehow wasn't bound (EnsureKostengruppeParameter
    /// couldn't run — e.g. no bindable category resolved at all — or a
    /// category outside BindableCategories slipped in via a manual
    /// "Kostengruppe" value on some other element type): Assembly Code
    /// (BuiltInParameter.UNIFORMAT_CODE / "Baugruppenkennzeichen" in German
    /// Revit — renamed to ASSEMBLY_CODE in the Revit 2026 API, same
    /// parameter, same underlying id, see the REVIT&lt;year&gt; symbol in
    /// Directory.Build.props) first, then Type Comments. Both are genuinely
    /// already in use on many projects for UniFormat/OmniClass or other
    /// classification, so this can overwrite existing data on that specific
    /// element — accepted as the tradeoff for "don't skip elements", per how
    /// this fallback was requested; the dedicated "Kostengruppe" parameter
    /// remains the preferred target precisely so this fallback stays a rare
    /// exception, not the common case.
    /// </summary>
    public static bool TryAssignKostengruppe(Element element, string code)
    {
        if (TrySetParameter(element.LookupParameter(ParameterOverrideName), code, allowInteger: true)) return true;
#if REVIT2026
        if (TrySetParameter(element.get_Parameter(BuiltInParameter.ASSEMBLY_CODE), code, allowInteger: false)) return true;
#else
        if (TrySetParameter(element.get_Parameter(BuiltInParameter.UNIFORMAT_CODE), code, allowInteger: false)) return true;
#endif
        return TrySetParameter(element.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS), code, allowInteger: false);
    }

    private static bool TrySetParameter(Parameter? param, string code, bool allowInteger)
    {
        if (param is not { IsReadOnly: false }) return false;

        return param.StorageType switch
        {
            StorageType.String => param.Set(code),
            StorageType.Integer when allowInteger => int.TryParse(code, out var intCode) && param.Set(intCode),
            _ => false,
        };
    }

    /// <summary>
    /// Makes sure the project has a "Kostengruppe" parameter bound across
    /// every category in BindableCategories before elements get assigned —
    /// so "Assign to elements" doesn't have to skip whichever elements
    /// happen to predate the parameter being added by hand. If it's already
    /// bound (by this tool on a previous run, or by hand via Manage →
    /// Project Parameters), this is a no-op and returns true immediately.
    ///
    /// Otherwise it creates a Text parameter through a small Ottawa Tools-owned
    /// shared parameter file (the only API-supported way to add a project
    /// parameter programmatically — Revit has no "create a pure project
    /// parameter" call) kept at a fixed per-user path
    /// (%AppData%\Ottawa Tools\Din276SharedParameters.txt) rather than a temp
    /// file, so re-running this on the same or a different project reuses
    /// the exact same parameter definition (same GUID) instead of minting a
    /// lookalike duplicate each time. The parameter still shows up under
    /// "Project Parameters" in Manage, same as any other.
    ///
    /// Application.SharedParametersFilename is a global, session-wide Revit
    /// setting — temporarily pointing it at this tool's own file could
    /// otherwise clobber a firm's own shared-parameter-file setting for the
    /// rest of the session, so the previous value is always restored
    /// afterward, success or failure, via try/finally.
    /// </summary>
    public static bool EnsureKostengruppeParameter(Document doc)
    {
        if (HasBinding(doc, ParameterOverrideName)) return true;

        var categorySet = doc.Application.Create.NewCategorySet();
        foreach (var builtIn in BindableCategories)
        {
            var category = Category.GetCategory(doc, builtIn);
            if (category is { AllowsBoundParameters: true }) categorySet.Insert(category);
        }
        if (categorySet.IsEmpty) return false;

        var app = doc.Application;
        var previousSharedParameterFile = app.SharedParametersFilename;
        try
        {
            app.SharedParametersFilename = OwnSharedParameterFilePath();
            var file = app.OpenSharedParameterFile();
            if (file is null) return false;

            var group = file.Groups.get_Item(SharedParameterGroupName) ?? file.Groups.Create(SharedParameterGroupName);

            var definition = group.Definitions.get_Item(ParameterOverrideName) as ExternalDefinition;
            definition ??= group.Definitions.Create(new ExternalDefinitionCreationOptions(ParameterOverrideName, SpecTypeId.String.Text)) as ExternalDefinition;
            if (definition is null) return false;

            var binding = app.Create.NewInstanceBinding(categorySet);
            return doc.ParameterBindings.Insert(definition, binding, GroupTypeId.Text);
        }
        finally
        {
            app.SharedParametersFilename = previousSharedParameterFile;
        }
    }

    private static bool HasBinding(Document doc, string parameterName)
    {
        var iterator = doc.ParameterBindings.ForwardIterator();
        while (iterator.MoveNext())
            if (iterator.Key.Name == parameterName) return true;
        return false;
    }

    private static string OwnSharedParameterFilePath()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ottawa Tools");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, "Din276SharedParameters.txt");
        if (!File.Exists(path))
            File.WriteAllText(path, "# This is a Revit shared parameter file.\r\n# Do not edit manually.\r\n*META\tVERSION\tMINVERSION\r\nMETA\t2\t1\r\n");

        return path;
    }

    private static double GetQuantity(Element element, QuantityUnit unit) => unit switch
    {
        QuantityUnit.Count => 1,
        QuantityUnit.SquareMeters => GetArea(element),
        QuantityUnit.CubicMeters => ConvertParam(element, BuiltInParameter.HOST_VOLUME_COMPUTED, UnitTypeId.CubicMeters),
        QuantityUnit.Meters => ConvertParam(element, BuiltInParameter.CURVE_ELEM_LENGTH, UnitTypeId.Meters),
        _ => 0,
    };

    private static double GetArea(Element element)
    {
        var p = element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
        if (p is not { HasValue: true }) p = element.get_Parameter(BuiltInParameter.ROOM_AREA);
        if (p is not { HasValue: true, StorageType: StorageType.Double }) return 0;
        return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.SquareMeters);
    }

    private static double ConvertParam(Element element, BuiltInParameter bip, ForgeTypeId unitType)
    {
        var p = element.get_Parameter(bip);
        if (p is not { HasValue: true, StorageType: StorageType.Double }) return 0;
        return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), unitType);
    }

    public static string UnitLabel(QuantityUnit unit) => unit switch
    {
        QuantityUnit.SquareMeters => "m²",
        QuantityUnit.CubicMeters => "m³",
        QuantityUnit.Meters => "m",
        _ => "Stk",
    };
}
