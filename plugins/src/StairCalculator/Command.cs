using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.StairCalculator;

/// <summary>
/// Checks every stair's actual riser height and tread depth (Revit's own
/// calculated instance parameters) against the well-known 2R+G ("two risers
/// plus a going") comfort guideline — a
/// design-proportion sanity check, not a certified code compliance review.
/// Actual code minimums/maximums vary by jurisdiction, so this is
/// deliberately framed as a rule-of-thumb flag, not a pass/fail code audit.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "staircalculator";
    private const double ComfortMinMm = 610.0;
    private const double ComfortMaxMm = 635.0;

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var stairs = new FilteredElementCollector(doc)
            .OfClass(typeof(Stairs))
            .Cast<Stairs>()
            .ToList();

        if (stairs.Count == 0)
        {
            TaskDialog.Show("BIMFlow — StairCalculator", "No stairs were found in this project.");
            return Result.Succeeded;
        }

        var rows = new List<ResultRow>();
        var stairsChecked = 0;

        foreach (var stair in stairs)
        {
            var riserParam = stair.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_RISER_HEIGHT);
            var treadParam = stair.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_TREAD_DEPTH);
            if (riserParam is null || !riserParam.HasValue || treadParam is null || !treadParam.HasValue)
                continue;

            var riserMm = UnitUtils.ConvertFromInternalUnits(riserParam.AsDouble(), UnitTypeId.Millimeters);
            var treadMm = UnitUtils.ConvertFromInternalUnits(treadParam.AsDouble(), UnitTypeId.Millimeters);

            stairsChecked++;
            var twoRPlusG = 2 * riserMm + treadMm;
            var withinComfortBand = twoRPlusG >= ComfortMinMm && twoRPlusG <= ComfortMaxMm;

            rows.Add(new ResultRow(
                new[]
                {
                    stair.Name,
                    riserMm.ToString("0") + " mm",
                    treadMm.ToString("0") + " mm",
                    twoRPlusG.ToString("0") + " mm",
                    withinComfortBand ? "Within 610-635mm band" : "Outside 610-635mm band",
                },
                new List<ElementId> { stair.Id }));
        }

        if (rows.Count == 0)
        {
            TaskDialog.Show("BIMFlow — StairCalculator", "Found stairs, but none had readable actual riser height/tread depth values.");
            return Result.Succeeded;
        }

        var flaggedCount = rows.Count(r => r.Cells[4].StartsWith("Outside"));

        var results = new ResultsListForm(
            "BIMFlow — StairCalculator Results",
            $"{stairsChecked} stair(s) checked, {flaggedCount} outside the 610-635mm 2R+G comfort band. " +
            "This is a design-proportion guideline, not a certified code compliance check — verify against your local building code.",
            new[] { "Stair", "Riser", "Tread", "2R+G", "Result" },
            rows,
            actionButtonText: "Select in model");

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }
}
