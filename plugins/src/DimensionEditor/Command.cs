using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using OttawaWork.Shared;

namespace OttawaWork.DimensionEditor;

/// <summary>
/// Lets you edit a dimension's override value, prefix, and suffix directly —
/// the write side of OverriddenDimensionDetector's read-only scan. Works on
/// both single- and multi-segment dimensions, one editable row per segment.
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
            updated = DimensionEditEngine.Apply(dimensions, edits);
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

    private class DimensionSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is Dimension;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
