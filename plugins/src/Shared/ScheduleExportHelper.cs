using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

/// <summary>
/// Exports a schedule to CSV the same way ViewSchedule.Export always has,
/// but first guarantees an Element ID column is present — added
/// temporarily if the schedule doesn't already have one, via a transaction
/// that's rolled back immediately after export, so the schedule's own
/// field list is never permanently changed. Revit's native schedule export
/// only ever includes whatever fields the schedule already displays, so
/// without this, Excel2Revit's import mode (which matches edited rows back
/// to elements by an ElementId column) has nothing to match on for a
/// schedule nobody set one up on — which in practice is most of them
/// (confirmed against a real project: none of its schedules had one).
/// </summary>
public static class ScheduleExportHelper
{
    public static void ExportWithElementId(Document doc, ViewSchedule schedule, string folder, string fileName, ViewScheduleExportOptions options)
    {
        var definition = schedule.Definition;
        var idParameterId = new ElementId(BuiltInParameter.ID_PARAM);

        var alreadyHasElementId = Enumerable.Range(0, definition.GetFieldCount())
            .Select(definition.GetField)
            .Any(f => f.HasSchedulableField && f.ParameterId == idParameterId);

        if (alreadyHasElementId)
        {
            schedule.Export(folder, fileName, options);
            return;
        }

        var idField = definition.GetSchedulableFields().FirstOrDefault(f => f.ParameterId == idParameterId);
        if (idField is null)
        {
            // This category doesn't expose Element ID as a schedulable field at all — export as-is.
            schedule.Export(folder, fileName, options);
            return;
        }

        using var transaction = new Transaction(doc, "BIMFlow: Temporary Element ID column for export");
        transaction.Start();
        try
        {
            definition.AddField(idField);
            schedule.Export(folder, fileName, options);
        }
        finally
        {
            transaction.RollBack();
        }
    }
}
