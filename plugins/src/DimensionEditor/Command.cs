using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using OttawaWork.Shared;

namespace OttawaWork.DimensionEditor;

/// <summary>
/// Lets you edit a dimension's override value, prefix, and suffix directly —
/// the write side of OverriddenDimensions' read-only audit. Works on both
/// single- and multi-segment dimensions, one editable row per segment.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : OttawaWorkCommand
{
    protected override string PluginSlug => "dimensioneditor";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var dimensions = uiDoc.Selection.GetElementIds()
            .Select(doc.GetElement)
            .OfType<Dimension>()
            .ToList();

        if (dimensions.Count == 0)
        {
            var references = uiDoc.Selection.PickObjects(
                ObjectType.Element,
                new DimensionSelectionFilter(),
                "Select dimensions to edit, then click Finish");
            dimensions = references.Select(r => doc.GetElement(r)).OfType<Dimension>().ToList();
        }

        if (dimensions.Count == 0)
        {
            TaskDialog.Show("Ottawa Tools — DimensionEditor", "No dimensions were selected.");
            return Result.Cancelled;
        }

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
                    rows.Add(new DimensionEditRow(
                        dimension.Id, index, viewName,
                        segment.ValueString ?? "",
                        segment.ValueOverride ?? "",
                        segment.Prefix ?? "",
                        segment.Suffix ?? ""));
                    index++;
                }
            }
            else
            {
                rows.Add(new DimensionEditRow(
                    dimension.Id, null, viewName,
                    dimension.ValueString ?? "",
                    dimension.ValueOverride ?? "",
                    dimension.Prefix ?? "",
                    dimension.Suffix ?? ""));
            }
        }

        var window = new DimensionEditorWindow(rows);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        var edits = window.BuildEdits();
        var updated = 0;

        using var transaction = new Transaction(doc, "Ottawa Tools: Edit Dimensions");
        transaction.Start();
        try
        {
            foreach (var dimension in dimensions)
            {
                if (dimension.NumberOfSegments > 1)
                {
                    var index = 1;
                    foreach (DimensionSegment segment in dimension.Segments)
                    {
                        var edit = edits.FirstOrDefault(e => e.DimensionId == dimension.Id && e.SegmentIndex == index);
                        if (edit is not null && ApplySegment(segment, edit)) updated++;
                        index++;
                    }
                }
                else
                {
                    var edit = edits.FirstOrDefault(e => e.DimensionId == dimension.Id && e.SegmentIndex is null);
                    if (edit is not null && ApplyDimension(dimension, edit)) updated++;
                }
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("Ottawa Tools — DimensionEditor", $"Updated {updated} of {rows.Count} dimension segment(s).");
        return Result.Succeeded;
    }

    private static bool ApplySegment(DimensionSegment segment, DimensionEditRow edit)
    {
        try
        {
            segment.ValueOverride = string.IsNullOrWhiteSpace(edit.Override) ? null : edit.Override;
            segment.Prefix = edit.Prefix;
            segment.Suffix = edit.Suffix;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool ApplyDimension(Dimension dimension, DimensionEditRow edit)
    {
        try
        {
            dimension.ValueOverride = string.IsNullOrWhiteSpace(edit.Override) ? null : edit.Override;
            dimension.Prefix = edit.Prefix;
            dimension.Suffix = edit.Suffix;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private class DimensionSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is Dimension;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
