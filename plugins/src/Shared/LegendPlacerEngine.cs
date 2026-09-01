using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

public enum LegendPositionMethod { CopyFromReferenceSheet, AnchorToTitleBlockCorner }
public enum LegendCorner { TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>
/// Position logic for batch-placing one legend view onto multiple sheets —
/// either by copying the exact on-sheet position from a sheet that already
/// has it placed, or by anchoring to a title block corner with an inset
/// offset (computed per sheet, since different sheets can use different
/// title block sizes/positions — a single global offset would only be
/// correct if every sheet's title block happened to be identical).
/// </summary>
public static class LegendPlacerEngine
{
    /// <summary>Sheets that already have the given legend placed, and the Viewport placing it there — a
    /// legend can only appear once per sheet, so this is a clean one-to-one map.</summary>
    public static Dictionary<ElementId, Viewport> ExistingPlacements(Document doc, ElementId legendId)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(Viewport))
            .Cast<Viewport>()
            .Where(vp => vp.ViewId == legendId)
            .GroupBy(vp => vp.SheetId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    public static XYZ? TitleBlockCornerPosition(Document doc, ElementId sheetId, LegendCorner corner, double offsetXFeet, double offsetYFeet)
    {
        var titleBlock = new FilteredElementCollector(doc, sheetId)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsNotElementType()
            .FirstOrDefault();
        var sheet = doc.GetElement(sheetId) as ViewSheet;
        var bbox = titleBlock?.get_BoundingBox(sheet);
        if (bbox is null) return null;

        return corner switch
        {
            LegendCorner.TopLeft => new XYZ(bbox.Min.X + offsetXFeet, bbox.Max.Y - offsetYFeet, 0),
            LegendCorner.TopRight => new XYZ(bbox.Max.X - offsetXFeet, bbox.Max.Y - offsetYFeet, 0),
            LegendCorner.BottomLeft => new XYZ(bbox.Min.X + offsetXFeet, bbox.Min.Y + offsetYFeet, 0),
            _ => new XYZ(bbox.Max.X - offsetXFeet, bbox.Min.Y + offsetYFeet, 0), // BottomRight
        };
    }
}
