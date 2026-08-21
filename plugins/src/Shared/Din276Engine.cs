using System.IO;
using Autodesk.Revit.DB;

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
/// extend the defaults per-project without touching this code; otherwise a
/// small built-in rule table maps common categories to their DIN 276 group
/// (Walls split into 330 Außenwände/340 Innenwände by WallFunction,
/// matching HighlightExterior/HighlightInterior's existing exterior/
/// interior split; doors/windows inherit their host wall's split). This
/// only covers the categories with an unambiguous DIN 276 home at the
/// 2nd-level — Columns/Framing and most nutzungsspezifische equipment are
/// deliberately left unmapped rather than guessed, and simply won't appear
/// in the report unless given an explicit Kostengruppe parameter value.
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

    public static readonly KostengruppeDef[] Kostengruppen =
    {
        new("310", "Baugrube", QuantityUnit.SquareMeters),
        new("320", "Gründung", QuantityUnit.SquareMeters),
        new("330", "Außenwände", QuantityUnit.SquareMeters),
        new("340", "Innenwände", QuantityUnit.SquareMeters),
        new("350", "Decken", QuantityUnit.SquareMeters),
        new("360", "Dächer", QuantityUnit.SquareMeters),
        new("410", "Abwasser-, Wasser-, Gasanlagen", QuantityUnit.Meters),
        new("420", "Wärmeversorgungsanlagen", QuantityUnit.Count),
        new("430", "Raumlufttechnische Anlagen", QuantityUnit.Meters),
        new("440", "Elektrische Anlagen", QuantityUnit.Count),
        new("450", "Kommunikations- und Sicherheitstechnik", QuantityUnit.Count),
        new("460", "Förderanlagen", QuantityUnit.Count),
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

            var def = Kostengruppen.FirstOrDefault(k => k.Code == code);
            if (def is null) continue;

            results.Add(new ElementQuantity(element.Id, code, GetQuantity(element, def.Unit)));
        }

        return results;
    }

    public static List<KostengruppeTotal> Aggregate(List<ElementQuantity> quantities, IReadOnlyDictionary<string, double> rates)
    {
        var totals = new List<KostengruppeTotal>();
        foreach (var def in Kostengruppen)
        {
            var matched = quantities.Where(q => q.Code == def.Code).ToList();
            if (matched.Count == 0) continue;

            var quantity = matched.Sum(q => q.Quantity);
            var rate = rates.TryGetValue(def.Code, out var r) ? r : 0;
            totals.Add(new KostengruppeTotal(def.Code, def.Name, def.Unit, quantity, rate, quantity * rate));
        }
        return totals;
    }

    private static string? ResolveCode(Element element, Dictionary<BuiltInCategory, string> categoryCodes)
    {
        var overrideValue = ReadOverrideParameter(element);
        if (!string.IsNullOrWhiteSpace(overrideValue)) return overrideValue;

        if (element is Wall wall)
            return wall.WallType?.Function == WallFunction.Interior ? "340" : "330";

        var category = element.Category;
        if (category is not null && (category.Id == new ElementId(BuiltInCategory.OST_Doors) || category.Id == new ElementId(BuiltInCategory.OST_Windows)))
        {
            var hostWall = (element as FamilyInstance)?.Host as Wall;
            return hostWall?.WallType?.Function == WallFunction.Interior ? "340" : "330";
        }

        if (category is null) return null;
        foreach (var (builtIn, code) in categoryCodes)
            if (category.Id == new ElementId(builtIn)) return code;

        return null;
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
    /// Revit) first, then Type Comments. Both are genuinely already in use
    /// on many projects for UniFormat/OmniClass or other classification, so
    /// this can overwrite existing data on that specific element — accepted
    /// as the tradeoff for "don't skip elements", per how this fallback was
    /// requested; the dedicated "Kostengruppe" parameter remains the
    /// preferred target precisely so this fallback stays a rare exception,
    /// not the common case.
    /// </summary>
    public static bool TryAssignKostengruppe(Element element, string code)
    {
        if (TrySetParameter(element.LookupParameter(ParameterOverrideName), code, allowInteger: true)) return true;
        if (TrySetParameter(element.get_Parameter(BuiltInParameter.UNIFORMAT_CODE), code, allowInteger: false)) return true;
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
