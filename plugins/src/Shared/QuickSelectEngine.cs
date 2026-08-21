using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace OttawaWork.Shared;

/// <summary>
/// Shared by the "Select" ribbon panel's 12 quick-select buttons (All/Ext/Int
/// Walls, All/Ext/Int Doors, Floors, Windows, Roofs, Ceilings, Beams, Rooms)
/// — matches the reference tool's Direct Selection panel: one click sets the
/// active Revit selection to every matching element visible in the active
/// view. No dialog on a normal hit (the selection highlighting itself, plus
/// Revit's own status-bar count, is the feedback — a modal popping up on
/// every click of what's meant to be a fast, repeatable selection tool would
/// defeat the point) and no transaction (Selection.SetElementIds is a UI-level
/// operation on UIDocument, not a document edit). Exterior/interior wall
/// filtering reuses the same WallType.Function check WallHighlighter already
/// uses; exterior/interior doors are classified by their host wall's
/// Function, since Door itself carries no Function parameter of its own.
/// </summary>
public static class QuickSelectEngine
{
    public enum Side { Any, Exterior, Interior }

    public static int SelectWalls(UIDocument uiDoc, Side side)
    {
        var doc = uiDoc.Document;
        var walls = new FilteredElementCollector(doc, doc.ActiveView.Id)
            .OfClass(typeof(Wall))
            .Cast<Wall>()
            .Where(w => Matches(w.WallType?.Function ?? WallFunction.Interior, side))
            .ToList();
        uiDoc.Selection.SetElementIds(walls.Select(w => w.Id).ToList());
        return walls.Count;
    }

    public static int SelectDoors(UIDocument uiDoc, Side side)
    {
        var doc = uiDoc.Document;
        var doors = new FilteredElementCollector(doc, doc.ActiveView.Id)
            .OfCategory(BuiltInCategory.OST_Doors)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .Where(d => side == Side.Any || Matches((d.Host as Wall)?.WallType?.Function ?? WallFunction.Interior, side))
            .ToList();
        uiDoc.Selection.SetElementIds(doors.Select(d => d.Id).ToList());
        return doors.Count;
    }

    public static int SelectCategory(UIDocument uiDoc, BuiltInCategory category)
    {
        var doc = uiDoc.Document;
        var ids = new FilteredElementCollector(doc, doc.ActiveView.Id)
            .OfCategory(category)
            .WhereElementIsNotElementType()
            .ToElementIds();
        uiDoc.Selection.SetElementIds(ids);
        return ids.Count;
    }

    private static bool Matches(WallFunction function, Side side) => side switch
    {
        Side.Exterior => function == WallFunction.Exterior,
        Side.Interior => function == WallFunction.Interior,
        _ => true,
    };
}
