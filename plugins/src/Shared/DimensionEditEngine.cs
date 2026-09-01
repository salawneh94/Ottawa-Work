using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

/// <summary>One editable row per dimension segment (or per dimension, for single-segment ones).</summary>
public record DimensionEditRow(ElementId DimensionId, int? SegmentIndex, string ViewName, string CurrentValue, string Override, string Prefix, string Suffix);

/// <summary>
/// The write side of a dimension edit: applies a set of edited rows back onto
/// the real Dimension/DimensionSegment elements. Pure element mutation, no
/// transaction of its own — the caller (a Command.cs) is expected to already
/// be inside one, same convention as RoomPlanGenerator.Generate/
/// TwoPassRenamer.Apply elsewhere in Shared. Shared between DimensionEditor
/// (edits whatever's already selected) and OverriddenDimensionDetector
/// (edits whatever the scan flagged) so the actual write logic — and the
/// DimensionEditorWindow dialog itself — isn't duplicated across plugins
/// that can't reference each other directly.
/// </summary>
public static class DimensionEditEngine
{
    public static int Apply(List<Dimension> dimensions, List<DimensionEditRow> edits)
    {
        var updated = 0;
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
        return updated;
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
}
