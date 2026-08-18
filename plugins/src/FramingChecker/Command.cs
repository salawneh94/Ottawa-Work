using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.FramingChecker;

/// <summary>
/// Flags structural framing members whose start or end point isn't near
/// any other structural element — a "floating member" that's a strong hint
/// of a missing connection. True connection-status checking would need the
/// analytical model, which the public API only exposes in limited ways;
/// this bounding-box proximity heuristic catches the common case (a beam
/// that isn't actually touching its support) without guessing at that API.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "framingchecker";
    private const double ToleranceFeet = 0.3;

    private static readonly BuiltInCategory[] StructuralCategories =
    {
        BuiltInCategory.OST_StructuralFraming,
        BuiltInCategory.OST_StructuralColumns,
        BuiltInCategory.OST_Walls,
        BuiltInCategory.OST_Floors,
        BuiltInCategory.OST_StructuralFoundation,
    };

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var framing = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType()
            .ToList();

        if (framing.Count == 0)
        {
            TaskDialog.Show("BIMFlow — FramingChecker", "No structural framing was found in this project.");
            return Result.Succeeded;
        }

        var allStructural = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .WherePasses(new ElementMulticategoryFilter(StructuralCategories))
            .ToList();

        var boxes = allStructural
            .Select(e => (Element: e, Box: e.get_BoundingBox(null)))
            .Where(t => t.Box is not null)
            .ToList();

        var rows = new List<ResultRow>();

        foreach (var member in framing)
        {
            if (member.Location is not LocationCurve locationCurve) continue;

            var start = locationCurve.Curve.GetEndPoint(0);
            var end = locationCurve.Curve.GetEndPoint(1);

            var startTouches = boxes.Any(b => b.Element.Id != member.Id && PointNearBox(start, b.Box!, ToleranceFeet));
            var endTouches = boxes.Any(b => b.Element.Id != member.Id && PointNearBox(end, b.Box!, ToleranceFeet));

            if (!startTouches || !endTouches)
            {
                var issue = !startTouches && !endTouches ? "Both ends floating" : !startTouches ? "Start end floating" : "End floating";
                var name = doc.GetElement(member.GetTypeId())?.Name ?? "Unknown type";
                rows.Add(new ResultRow(new[] { name, $"Id {member.Id.Value}", issue }, new List<ElementId> { member.Id }));
            }
        }

        if (rows.Count == 0)
        {
            TaskDialog.Show("BIMFlow — FramingChecker", $"All {framing.Count} framing member(s) have both ends near another structural element.");
            return Result.Succeeded;
        }

        var results = new ResultsListForm(
            "BIMFlow — FramingChecker Results",
            $"{rows.Count} of {framing.Count} framing member(s) may be missing a connection.",
            new[] { "Type", "Element Id", "Issue" },
            rows);

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }

    private static bool PointNearBox(XYZ point, BoundingBoxXYZ box, double tolerance)
    {
        return point.X >= box.Min.X - tolerance && point.X <= box.Max.X + tolerance
            && point.Y >= box.Min.Y - tolerance && point.Y <= box.Max.Y + tolerance
            && point.Z >= box.Min.Z - tolerance && point.Z <= box.Max.Z + tolerance;
    }
}
