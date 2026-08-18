using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.QCSummary;

/// <summary>
/// Runs a set of model-wide QA checks in one pass: unbounded/unenclosed
/// rooms, zero-length walls (plus a heuristic flag for walls with an
/// unconnected end — a bounding-box proximity test, not true geometric
/// connectivity analysis), doors/windows whose host was deleted out from
/// under them, any user-flagged forbidden categories, and window sill
/// heights that don't match the rest of their level.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "qcsummary";
    private const double ZeroLengthToleranceFeet = 0.05;
    private const double ConnectionToleranceFeet = 0.1;
    private const double SillDeviationToleranceFeet = 2.0 / 12.0;

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var modelCategories = doc.Settings.Categories
            .Cast<Category>()
            .Where(c => c.CategoryType == CategoryType.Model && c.AllowsBoundParameters)
            .OrderBy(c => c.Name)
            .ToList();

        var categoryWindow = new ForbiddenCategoryWindow(modelCategories);
        if (categoryWindow.ShowDialog() != true)
            return Result.Cancelled;

        var rows = new List<ResultRow>();

        CheckUnenclosedRooms(doc, rows);
        CheckWalls(doc, rows);
        CheckOrphanedHosts(doc, rows);
        CheckForbiddenCategories(doc, categoryWindow.ForbiddenCategories, rows);
        CheckSillConsistency(doc, rows);

        if (rows.Count == 0)
        {
            TaskDialog.Show("BIMFlow — QCSummary", "No issues found across rooms, walls, doors/windows, forbidden categories, or sill heights.");
            return Result.Succeeded;
        }

        var results = new ResultsListForm(
            "BIMFlow — QCSummary Results",
            $"{rows.Count} issue(s) found across the checks below.",
            new[] { "Check", "Element", "Detail" },
            rows,
            actionButtonText: "Select in model");

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }

    private static void CheckUnenclosedRooms(Document doc, List<ResultRow> rows)
    {
        var rooms = new FilteredElementCollector(doc)
            .OfClass(typeof(SpatialElement))
            .OfType<Room>();

        foreach (var room in rooms)
        {
            if (room.Area > 0) continue;
            rows.Add(new ResultRow(
                new[] { "Unbounded room", room.Name, "Room is placed but not enclosed (zero area)." },
                new List<ElementId> { room.Id }));
        }
    }

    private static void CheckWalls(Document doc, List<ResultRow> rows)
    {
        var walls = new FilteredElementCollector(doc)
            .OfClass(typeof(Wall))
            .Cast<Wall>()
            .ToList();

        var endpoints = walls
            .Select(w => (wall: w, curve: w.Location as LocationCurve))
            .Where(t => t.curve is not null)
            .ToList();

        foreach (var (wall, curve) in endpoints)
        {
            var line = curve!.Curve;
            if (line.Length < ZeroLengthToleranceFeet)
            {
                rows.Add(new ResultRow(
                    new[] { "Zero-length wall", wall.Name, "Wall's location curve is effectively zero length." },
                    new List<ElementId> { wall.Id }));
                continue;
            }

            var start = line.GetEndPoint(0);
            var end = line.GetEndPoint(1);
            var startConnected = endpoints.Any(o => o.wall.Id != wall.Id && EndpointNear(o.curve!.Curve, start));
            var endConnected = endpoints.Any(o => o.wall.Id != wall.Id && EndpointNear(o.curve!.Curve, end));

            if (!startConnected || !endConnected)
            {
                rows.Add(new ResultRow(
                    new[] { "Possibly disconnected wall", wall.Name, "No neighboring wall endpoint found within tolerance at one or both ends (heuristic — verify visually)." },
                    new List<ElementId> { wall.Id }));
            }
        }
    }

    private static bool EndpointNear(Curve other, XYZ point)
    {
        return other.GetEndPoint(0).DistanceTo(point) < ConnectionToleranceFeet
            || other.GetEndPoint(1).DistanceTo(point) < ConnectionToleranceFeet;
    }

    private static void CheckOrphanedHosts(Document doc, List<ResultRow> rows)
    {
        var categories = new[] { BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows };
        foreach (var bic in categories)
        {
            var instances = new FilteredElementCollector(doc)
                .OfCategory(bic)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>();

            foreach (var instance in instances)
            {
                if (instance.Host is not null) continue;
                rows.Add(new ResultRow(
                    new[] { "Missing host", instance.Name, $"{bic.ToString().Replace("OST_", "")} has no host element." },
                    new List<ElementId> { instance.Id }));
            }
        }
    }

    private static void CheckForbiddenCategories(Document doc, List<Category> forbidden, List<ResultRow> rows)
    {
        foreach (var category in forbidden)
        {
            var instances = new FilteredElementCollector(doc)
                .OfCategoryId(category.Id)
                .WhereElementIsNotElementType();

            foreach (var element in instances)
            {
                rows.Add(new ResultRow(
                    new[] { "Forbidden category", element.Name, $"Category \"{category.Name}\" was flagged as not allowed in this model." },
                    new List<ElementId> { element.Id }));
            }
        }
    }

    private static void CheckSillConsistency(Document doc, List<ResultRow> rows)
    {
        var windows = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Windows)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Select(w => (window: w, sill: w.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)))
            .Where(t => t.sill is not null)
            .ToList();

        foreach (var levelGroup in windows.GroupBy(t => t.window.LevelId))
        {
            var sills = levelGroup.Select(t => t.sill!.AsDouble()).OrderBy(v => v).ToList();
            var median = sills[sills.Count / 2];

            foreach (var (window, sill) in levelGroup)
            {
                var value = sill!.AsDouble();
                if (Math.Abs(value - median) <= SillDeviationToleranceFeet) continue;

                var level = doc.GetElement(window.LevelId) as Level;
                rows.Add(new ResultRow(
                    new[]
                    {
                        "Sill height inconsistency", window.Name,
                        $"Sill height on {level?.Name ?? "unknown level"} deviates from that level's median by more than 2\".",
                    },
                    new List<ElementId> { window.Id }));
            }
        }
    }
}
