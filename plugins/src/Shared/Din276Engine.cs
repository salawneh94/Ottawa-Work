using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

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
/// shared parameter on the element wins if present (e.g. "331"), letting a
/// firm override or extend the defaults per-project without touching this
/// code; otherwise a small built-in rule table maps common categories to
/// their DIN 276 group (Walls split into 330 Außenwände/340 Innenwände by
/// WallFunction, matching HighlightExterior/HighlightInterior's existing
/// exterior/interior split; doors/windows inherit their host wall's split).
/// This only covers the categories with an unambiguous DIN 276 home at the
/// 2nd-level — Columns/Framing and most nutzungsspezifische equipment are
/// deliberately left unmapped rather than guessed, and simply won't appear
/// in the report unless given an explicit Kostengruppe parameter value.
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
        var overrideParam = element.LookupParameter(ParameterOverrideName);
        if (overrideParam is { HasValue: true, StorageType: StorageType.String } && !string.IsNullOrWhiteSpace(overrideParam.AsString()))
            return overrideParam.AsString();

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
