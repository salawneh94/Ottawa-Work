using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.DimensionAuto;

/// <summary>
/// Auto-dimensions grid-to-grid spacing in the active view — the one
/// dimensioning target with clean, unambiguous references (each grid is a
/// straight line with a well-defined position). Wall-face and opening
/// dimensioning need real geometry analysis per wall type/layer, which
/// isn't something to guess at without testing against real models.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "dimensionauto";
    private const double OffsetFeet = 10.0;

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var grids = new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(Grid))
            .Cast<Grid>()
            .Where(g => g.Curve is Line)
            .ToList();

        if (grids.Count < 2)
        {
            TaskDialog.Show("BIMFlow — DimensionAuto", "Need at least two straight grids visible in the active view.");
            return Result.Succeeded;
        }

        var vertical = new List<(Grid Grid, Line Line)>();
        var horizontal = new List<(Grid Grid, Line Line)>();

        foreach (var grid in grids)
        {
            var line = (Line)grid.Curve;
            var dir = (line.GetEndPoint(1) - line.GetEndPoint(0));
            if (Math.Abs(dir.X) < Math.Abs(dir.Y))
                vertical.Add((grid, line));
            else
                horizontal.Add((grid, line));
        }

        var placed = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Auto-Dimension Grids");
        transaction.Start();
        try
        {
            if (vertical.Count >= 2 && TryPlaceDimension(doc, view, vertical, isVertical: true))
                placed++;
            if (horizontal.Count >= 2 && TryPlaceDimension(doc, view, horizontal, isVertical: false))
                placed++;

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "BIMFlow — DimensionAuto",
            placed > 0
                ? $"Placed {placed} grid dimension string(s) in the active view."
                : "Couldn't place a dimension string — the grids may not support dimensioning in this view type.");

        return Result.Succeeded;
    }

    private static bool TryPlaceDimension(Document doc, View view, List<(Grid Grid, Line Line)> grids, bool isVertical)
    {
        try
        {
            var refArray = new ReferenceArray();
            double minA = double.MaxValue, maxA = double.MinValue, minB = double.MaxValue;

            var ordered = grids
                .Select(g => new
                {
                    g.Grid,
                    Mid = g.Line.Evaluate(0.5, true),
                    MinEnd = g.Line.GetEndPoint(0),
                    MaxEnd = g.Line.GetEndPoint(1),
                })
                .OrderBy(g => isVertical ? g.Mid.X : g.Mid.Y)
                .ToList();

            foreach (var g in ordered)
            {
                refArray.Append(new Reference(g.Grid));
                var a = isVertical ? g.Mid.X : g.Mid.Y;
                var bLow = Math.Min(g.MinEnd.Y, g.MaxEnd.Y);
                var bLowH = Math.Min(g.MinEnd.X, g.MaxEnd.X);
                minA = Math.Min(minA, a);
                maxA = Math.Max(maxA, a);
                minB = Math.Min(minB, isVertical ? bLow : bLowH);
            }

            var offsetB = minB - OffsetFeet;

            var dimLine = isVertical
                ? Line.CreateBound(new XYZ(minA - 2, offsetB, 0), new XYZ(maxA + 2, offsetB, 0))
                : Line.CreateBound(new XYZ(offsetB, minA - 2, 0), new XYZ(offsetB, maxA + 2, 0));

            doc.Create.NewDimension(view, dimLine, refArray);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
