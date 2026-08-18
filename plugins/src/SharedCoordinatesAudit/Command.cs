using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.SharedCoordinatesAudit;

/// <summary>
/// For every loaded link, transforms its survey point into this project's
/// coordinate system and compares it to this project's own survey point —
/// the same real-world monument should land in the same place if the link
/// was positioned by shared coordinates rather than dragged into place or
/// left at origin-to-origin. Also compares Angle to True North where that
/// parameter is readable on both documents. This checks alignment, not
/// which positioning mode Revit used originally — that isn't exposed on
/// the link after the fact.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "sharedcoordinatesaudit";
    private const double PositionToleranceFeet = 0.5; // ~6"
    private const double AngleToleranceDegrees = 0.1;

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        BasePoint? hostSurveyPoint;
        try
        {
            hostSurveyPoint = BasePoint.GetSurveyPoint(doc);
        }
        catch (Exception)
        {
            hostSurveyPoint = null;
        }

        if (hostSurveyPoint is null)
        {
            TaskDialog.Show("BIMFlow — SharedCoordinatesAudit", "Couldn't read this project's survey point.");
            return Result.Succeeded;
        }

        var hostSurveyPosition = hostSurveyPoint.Position;
        var hostAngle = TryGetAngleToTrueNorth(doc);

        var linkInstances = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .ToList();

        if (linkInstances.Count == 0)
        {
            TaskDialog.Show("BIMFlow — SharedCoordinatesAudit", "No linked models are in this project.");
            return Result.Succeeded;
        }

        var rows = new List<ResultRow>();

        foreach (var link in linkInstances)
        {
            var linkType = doc.GetElement(link.GetTypeId()) as RevitLinkType;
            var status = linkType?.GetLinkedFileStatus() ?? LinkedFileStatus.Invalid;
            var linkDoc = link.GetLinkDocument();

            if (status != LinkedFileStatus.Loaded || linkDoc is null)
            {
                rows.Add(new ResultRow(
                    new[] { link.Name, "Unloaded", "Link isn't loaded — can't check its coordinates." },
                    new List<ElementId> { link.Id }));
                continue;
            }

            try
            {
                var linkSurveyPoint = BasePoint.GetSurveyPoint(linkDoc);
                if (linkSurveyPoint is null)
                {
                    rows.Add(new ResultRow(
                        new[] { link.Name, "Unknown", "Couldn't read this link's survey point." },
                        new List<ElementId> { link.Id }));
                    continue;
                }

                var transform = link.GetTotalTransform();
                var linkSurveyInHost = transform.OfPoint(linkSurveyPoint.Position);
                var offsetFeet = linkSurveyInHost.DistanceTo(hostSurveyPosition);

                var linkAngle = TryGetAngleToTrueNorth(linkDoc);
                double? angleDiff = hostAngle.HasValue && linkAngle.HasValue
                    ? NormalizeDegrees(hostAngle.Value - linkAngle.Value)
                    : null;

                var aligned = offsetFeet <= PositionToleranceFeet && (!angleDiff.HasValue || angleDiff.Value <= AngleToleranceDegrees);
                var offsetMeters = UnitUtils.ConvertFromInternalUnits(offsetFeet, UnitTypeId.Meters);

                var detail = $"Survey point offset: {offsetMeters:0.00} m" +
                    (angleDiff.HasValue ? $"; True North difference: {angleDiff.Value:0.0}°" : "; True North not comparable");

                rows.Add(new ResultRow(
                    new[] { link.Name, aligned ? "Aligned" : "Misaligned", detail },
                    new List<ElementId> { link.Id }));
            }
            catch (Exception)
            {
                rows.Add(new ResultRow(
                    new[] { link.Name, "Unknown", "Couldn't compute this link's shared-coordinate alignment." },
                    new List<ElementId> { link.Id }));
            }
        }

        var misalignedCount = rows.Count(r => r.Cells[1] == "Misaligned");

        var results = new ResultsListForm(
            "BIMFlow — SharedCoordinatesAudit Results",
            $"{linkInstances.Count} link(s) checked, {misalignedCount} misaligned with this project's shared coordinates.",
            new[] { "Link", "Status", "Detail" },
            rows,
            actionButtonText: "Select in model");

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }

    private static double? TryGetAngleToTrueNorth(Document targetDoc)
    {
        try
        {
            var projectBasePoint = BasePoint.GetProjectBasePoint(targetDoc);
            var param = projectBasePoint?.get_Parameter(BuiltInParameter.BASEPOINT_ANGLETON_PARAM);
            return param is not null ? param.AsDouble() * (180.0 / Math.PI) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static double NormalizeDegrees(double degrees)
    {
        degrees %= 360;
        if (degrees > 180) degrees -= 360;
        if (degrees < -180) degrees += 360;
        return Math.Abs(degrees);
    }
}
