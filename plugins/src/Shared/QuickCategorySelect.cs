using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIMFlow.Shared;

/// <summary>
/// One-click "select every X in the active view" logic, factored out of
/// SelectByCategory/SelectExteriorInterior so the quick-access ribbon's
/// single-category buttons (no picker dialog — the category is baked into
/// which button you clicked) can reuse the exact same collection and
/// exterior/interior-by-host-wall logic instead of duplicating it 11 times.
/// </summary>
public static class QuickCategorySelect
{
    public static Result SelectAll(UIDocument uiDoc, BuiltInCategory category, string label)
    {
        var doc = uiDoc.Document;
        var view = doc.ActiveView;
        var wholeProjectScope = view is View3D;

        var collector = wholeProjectScope
            ? new FilteredElementCollector(doc)
            : new FilteredElementCollector(doc, view.Id);

        var ids = collector
            .OfCategory(category)
            .WhereElementIsNotElementType()
            .Select(e => e.Id)
            .ToList();

        if (ids.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Quick Select", $"No {label} found in the active view.");
            return Result.Succeeded;
        }

        uiDoc.Selection.SetElementIds(ids);
        return Result.Succeeded;
    }

    /// <summary>Selects elements of <paramref name="category"/> whose host wall's Function matches (walls themselves, or door/window instances hosted in one).</summary>
    public static Result SelectByWallFunction(UIDocument uiDoc, BuiltInCategory category, WallFunction function, string label)
    {
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var elements = new FilteredElementCollector(doc, view.Id)
            .OfCategory(category)
            .WhereElementIsNotElementType()
            .ToList();

        var matched = elements
            .Where(e => GetWallFunction(doc, e) == function)
            .Select(e => e.Id)
            .ToList();

        if (matched.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Quick Select", $"No {label} found in the active view.");
            return Result.Succeeded;
        }

        uiDoc.Selection.SetElementIds(matched);
        return Result.Succeeded;
    }

    private static WallFunction? GetWallFunction(Document doc, Element element)
    {
        var wall = element as Wall ?? (element as FamilyInstance)?.Host as Wall;
        var wallType = wall is null ? null : doc.GetElement(wall.GetTypeId()) as WallType;
        return wallType?.Function;
    }
}
