using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.OverriddenDimensions;

/// <summary>
/// Scans every dimension in the model for a manually typed value override
/// on any segment — a dimension that shows a number the model geometry
/// doesn't actually produce, which is easy to miss scrolling through sheets
/// by eye.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "overriddendimensions";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var dimensions = new FilteredElementCollector(doc)
            .OfClass(typeof(Dimension))
            .Cast<Dimension>()
            .ToList();

        if (dimensions.Count == 0)
        {
            TaskDialog.Show("BIMFlow — OverriddenDimensions", "No dimensions were found in this project.");
            return Result.Succeeded;
        }

        var rows = new List<ResultRow>();

        foreach (var dimension in dimensions)
        {
            var overrides = new List<string>();

            if (dimension.NumberOfSegments > 1)
            {
                var index = 1;
                foreach (DimensionSegment segment in dimension.Segments)
                {
                    if (!string.IsNullOrEmpty(segment.ValueOverride))
                        overrides.Add($"Segment {index}: \"{segment.ValueOverride}\"");
                    index++;
                }
            }
            else if (!string.IsNullOrEmpty(dimension.ValueOverride))
            {
                overrides.Add($"\"{dimension.ValueOverride}\"");
            }

            if (overrides.Count == 0) continue;

            var ownerView = doc.GetElement(dimension.OwnerViewId) as View;
            rows.Add(new ResultRow(
                new[] { dimension.Id.ToString(), ownerView?.Name ?? "(unknown view)", string.Join("; ", overrides) },
                new List<ElementId> { dimension.Id }));
        }

        if (rows.Count == 0)
        {
            TaskDialog.Show("BIMFlow — OverriddenDimensions", $"Checked {dimensions.Count} dimension(s). None have a value override.");
            return Result.Succeeded;
        }

        var results = new ResultsListForm(
            "BIMFlow — OverriddenDimensions Results",
            $"{rows.Count} of {dimensions.Count} dimension(s) have a manual value override.",
            new[] { "Dimension Id", "View", "Override(s)" },
            rows,
            actionButtonText: "Select in model");

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }
}
