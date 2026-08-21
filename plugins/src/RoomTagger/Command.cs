using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.RoomTagger;

/// <summary>
/// Batch-writes each room's number/name onto every element found inside it
/// (via the same point-in-room test RoomInventory uses for its report) — for
/// categories that don't get Revit's automatic Room field populated, which
/// covers most MEP equipment/fixtures and every line-based MEP system
/// (ducts, pipes, cable trays, conduits never get one at all). Point-based
/// elements use their location point directly; line-based ones use their
/// location curve's midpoint as the representative test point.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "roomtagger";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var rooms = new FilteredElementCollector(doc)
            .OfClass(typeof(SpatialElement))
            .OfType<Room>()
            .Where(r => r.Area > 0)
            .ToList();

        if (rooms.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — RoomTagger", "No placed rooms were found in this project.");
            return Result.Succeeded;
        }

        var window = new RoomTaggerWindow();
        if (window.ShowDialog() != true || window.SelectedCategories.Count == 0)
            return Result.Cancelled;

        if (string.IsNullOrWhiteSpace(window.RoomNumberParameter) && string.IsNullOrWhiteSpace(window.RoomNameParameter))
        {
            TaskDialog.Show("Ottawa Tools — RoomTagger", "Enter at least one target parameter name.");
            return Result.Cancelled;
        }

        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .WherePasses(new ElementMulticategoryFilter(window.SelectedCategories))
            .Select(e => (Element: e, Point: RepresentativePoint(e)))
            .Where(t => t.Point is not null)
            .ToList();

        var tagged = 0;
        var noRoom = 0;
        var noParam = 0;

        using var transaction = new Transaction(doc, "Ottawa Tools: Tag Elements With Room");
        transaction.Start();
        try
        {
            foreach (var (element, point) in elements)
            {
                var room = rooms.FirstOrDefault(r => r.IsPointInRoom(point!));
                if (room is null)
                {
                    noRoom++;
                    continue;
                }

                var wroteAny = false;

                if (!string.IsNullOrWhiteSpace(window.RoomNumberParameter) && TrySetText(element, window.RoomNumberParameter, room.Number))
                    wroteAny = true;

                if (!string.IsNullOrWhiteSpace(window.RoomNameParameter) && TrySetText(element, window.RoomNameParameter, room.Name))
                    wroteAny = true;

                if (wroteAny) tagged++;
                else noParam++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "Ottawa Tools — RoomTagger",
            $"Tagged {tagged} element(s) with their room.\n" +
            $"{noRoom} element(s) weren't inside any room.\n" +
            $"{noParam} element(s) were inside a room but had neither target parameter — check the parameter names are spelled right and bound to these categories.");

        return Result.Succeeded;
    }

    private static bool TrySetText(Element element, string parameterName, string value)
    {
        var parameter = element.LookupParameter(parameterName);
        if (parameter is not { IsReadOnly: false, StorageType: StorageType.String }) return false;

        parameter.Set(value);
        return true;
    }

    private static XYZ? RepresentativePoint(Element element) => element.Location switch
    {
        LocationPoint locationPoint => locationPoint.Point,
        LocationCurve locationCurve => locationCurve.Curve.Evaluate(0.5, true),
        _ => null,
    };
}
