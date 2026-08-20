using Autodesk.Revit.DB;
using Autodesk.Revit.DB.PointClouds;

namespace BIMFlow.Shared;

public enum HeatmapStatus { Ok, Monitor, Review, NoCoverage }

public record WallHeatmapResult(ElementId WallId, string WallName, int PointCount, int PenetratingCount, double DeviationPercent, HeatmapStatus Status);

/// <summary>
/// Compares each wall's modeled position against nearby point cloud scan
/// data to flag as-built deviations — walls that are further out of
/// tolerance than expected get a worse status. For each wall, samples
/// scan points in a box around its footprint (via
/// PointCloudFilterFactory.CreateMultiPlaneFilter, a set of half-space
/// planes — each plane's normal points outward, away from the kept
/// region, per Revit API convention), then measures how far each sampled
/// point sits from the wall's centerline along its own thickness
/// direction (Wall.Orientation). A point within half the wall's
/// thickness plus a small tolerance of the centerline is "on the wall";
/// anything further out counts as a deviation. Deviation % is the share
/// of sampled points that deviate.
///
/// Known simplification: doesn't exclude points that fall inside door/
/// window openings, so a wall with large openings may read a higher
/// deviation % than it should — worth checking real results against this
/// before trusting the Review bucket blindly.
/// </summary>
public static class PointCloudHeatmapAnalyzer
{
    private const double ToleranceFeet = 0.15;
    private const double MarginFeet = 1.0;
    private const double AverageDistanceFeet = 0.05;
    private const int MaxPointsPerCloud = 20000;

    public static List<WallHeatmapResult> Analyze(IList<Wall> walls, IList<PointCloudInstance> pointClouds)
    {
        return walls.Select(wall => AnalyzeWall(wall, pointClouds)).ToList();
    }

    private static WallHeatmapResult AnalyzeWall(Wall wall, IList<PointCloudInstance> pointClouds)
    {
        var name = wall.Name;

        if (wall.Location is not LocationCurve locationCurve || locationCurve.Curve is not Line line)
            return new WallHeatmapResult(wall.Id, name, 0, 0, 0, HeatmapStatus.NoCoverage);

        var bbox = wall.get_BoundingBox(null);
        if (bbox is null)
            return new WallHeatmapResult(wall.Id, name, 0, 0, 0, HeatmapStatus.NoCoverage);

        var start = line.GetEndPoint(0);
        var end = line.GetEndPoint(1);
        var direction = (end - start).Normalize();
        var orientation = wall.Orientation.Normalize();
        var halfWidth = wall.Width / 2.0;

        var planes = new List<Plane>
        {
            Plane.CreateByNormalAndOrigin(direction, end + direction * MarginFeet),
            Plane.CreateByNormalAndOrigin(-direction, start - direction * MarginFeet),
            Plane.CreateByNormalAndOrigin(orientation, start + orientation * (halfWidth + MarginFeet)),
            Plane.CreateByNormalAndOrigin(-orientation, start - orientation * (halfWidth + MarginFeet)),
            Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(start.X, start.Y, bbox.Max.Z + MarginFeet)),
            Plane.CreateByNormalAndOrigin(-XYZ.BasisZ, new XYZ(start.X, start.Y, bbox.Min.Z - MarginFeet)),
        };
        var filter = PointCloudFilterFactory.CreateMultiPlaneFilter(planes);

        var totalPoints = 0;
        var penetrating = 0;

        foreach (var cloud in pointClouds)
        {
            PointCollection points;
            try { points = cloud.GetPoints(filter, AverageDistanceFeet, MaxPointsPerCloud); }
            catch { continue; }

            var transform = cloud.GetTransform();
            foreach (var cloudPoint in points)
            {
                var world = transform.OfPoint(new XYZ(cloudPoint.X, cloudPoint.Y, cloudPoint.Z));
                var distanceAlongNormal = (world - start).DotProduct(orientation);

                totalPoints++;
                if (Math.Abs(distanceAlongNormal) > halfWidth + ToleranceFeet)
                    penetrating++;
            }
        }

        if (totalPoints == 0)
            return new WallHeatmapResult(wall.Id, name, 0, 0, 0, HeatmapStatus.NoCoverage);

        var deviationPercent = 100.0 * penetrating / totalPoints;
        var status = deviationPercent <= 35 ? HeatmapStatus.Ok
            : deviationPercent <= 65 ? HeatmapStatus.Monitor
            : HeatmapStatus.Review;

        return new WallHeatmapResult(wall.Id, name, totalPoints, penetrating, deviationPercent, status);
    }
}
