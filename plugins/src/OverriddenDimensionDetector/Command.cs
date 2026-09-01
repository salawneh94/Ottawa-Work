using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OttawaWork.Shared;

namespace OttawaWork.OverriddenDimensionDetector;

/// <summary>
/// Scans the model for overridden/annotated dimensions, shows them
/// classified by severity, and either selects the flagged ones in the model
/// or opens the shared DimensionEditorWindow (same one DimensionEditor uses)
/// to fix them — the detect side of that plugin's edit side.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "overriddendimensiondetector";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var window = new OverriddenDimensionDetectorWindow(doc);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.Action == DetectorAction.Select)
        {
            var ids = window.TargetRows.Select(r => r.DimensionId).Distinct().ToList();
            uiDoc.Selection.SetElementIds(ids);
            return Result.Succeeded;
        }

        // Fix: re-fetch the real Dimension/DimensionSegment data for exactly
        // the flagged (dimension, segment) pairs the detector found — not
        // whatever else is on those dimensions — and hand it to the same
        // edit dialog/apply logic DimensionEditor uses, so there's one
        // write path for dimension edits, not two.
        var targetKeys = window.TargetRows.Select(r => (r.DimensionId, r.SegmentIndex)).ToHashSet();
        var dimensions = targetKeys.Select(k => k.DimensionId).Distinct()
            .Select(id => doc.GetElement(id) as Dimension)
            .OfType<Dimension>()
            .ToList();

        var rows = new List<DimensionEditRow>();
        foreach (var dimension in dimensions)
        {
            var ownerView = doc.GetElement(dimension.OwnerViewId) as View;
            var viewName = ownerView?.Name ?? "(unknown view)";

            if (dimension.NumberOfSegments > 1)
            {
                var index = 1;
                foreach (DimensionSegment segment in dimension.Segments)
                {
                    if (targetKeys.Contains((dimension.Id, index)))
                    {
                        rows.Add(new DimensionEditRow(
                            dimension.Id, index, viewName,
                            segment.ValueString ?? "",
                            segment.ValueOverride ?? "",
                            segment.Prefix ?? "",
                            segment.Suffix ?? ""));
                    }
                    index++;
                }
            }
            else if (targetKeys.Contains((dimension.Id, null)))
            {
                rows.Add(new DimensionEditRow(
                    dimension.Id, null, viewName,
                    dimension.ValueString ?? "",
                    dimension.ValueOverride ?? "",
                    dimension.Prefix ?? "",
                    dimension.Suffix ?? ""));
            }
        }

        if (rows.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — Overridden Dimension Detector", "Nothing to fix — the flagged dimensions couldn't be re-read from the model.");
            return Result.Succeeded;
        }

        var editWindow = new DimensionEditorWindow(rows);
        if (editWindow.ShowDialog() != true)
            return Result.Cancelled;

        var edits = editWindow.BuildEdits();
        int updated;

        using var transaction = new Transaction(doc, "Ottawa Tools: Fix Overridden Dimensions");
        transaction.Start();
        try
        {
            updated = DimensionEditEngine.Apply(dimensions, edits);
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("Ottawa Tools — Overridden Dimension Detector", $"Updated {updated} of {rows.Count} dimension segment(s).");
        return Result.Succeeded;
    }
}
