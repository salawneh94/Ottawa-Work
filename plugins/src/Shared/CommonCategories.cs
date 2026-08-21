using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

/// <summary>
/// A fixed roster of common model categories, always offered in the same
/// order regardless of what's actually in the model — shared by every
/// plugin dialog that offers a "pick a category" dropdown (Color Code,
/// Batch Excel Sync) so they stay consistent with each other instead of
/// each keeping a separate, slowly-drifting copy of the same list.
/// </summary>
public static class CommonCategories
{
    public static readonly (string Label, BuiltInCategory BuiltIn)[] Roster =
    {
        ("Walls", BuiltInCategory.OST_Walls),
        ("Doors", BuiltInCategory.OST_Doors),
        ("Windows", BuiltInCategory.OST_Windows),
        ("Rooms", BuiltInCategory.OST_Rooms),
        ("Floors", BuiltInCategory.OST_Floors),
        ("Ceilings", BuiltInCategory.OST_Ceilings),
        ("Furniture", BuiltInCategory.OST_Furniture),
        ("Generic Models", BuiltInCategory.OST_GenericModel),
        ("Structural Columns", BuiltInCategory.OST_StructuralColumns),
        ("Structural Framing", BuiltInCategory.OST_StructuralFraming),
        ("Mechanical Equipment", BuiltInCategory.OST_MechanicalEquipment),
        ("Electrical Equipment", BuiltInCategory.OST_ElectricalEquipment),
        ("Plumbing Fixtures", BuiltInCategory.OST_PlumbingFixtures),
        ("Casework", BuiltInCategory.OST_Casework),
        ("Curtain Panels", BuiltInCategory.OST_CurtainWallPanels),
        ("Curtain Wall Mullions", BuiltInCategory.OST_CurtainWallMullions),
        ("Columns", BuiltInCategory.OST_Columns),
        ("Roofs", BuiltInCategory.OST_Roofs),
        ("Stairs", BuiltInCategory.OST_Stairs),
        ("Railings", BuiltInCategory.OST_Railings),
    };
}
