using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

/// <summary>
/// Exports a schedule to CSV the same way ViewSchedule.Export always has,
/// but first guarantees an Element ID column with a known, literal
/// "ElementId" heading is present — added temporarily if the schedule
/// doesn't already have that field, and its heading force-set either way.
///
/// This has failed silently — same "no ElementId column" outcome — across
/// several different attempts (adding the field, forcing ColumnHeading,
/// forcing Document.Regenerate(), committing via a TransactionGroup
/// instead of rolling back a plain transaction). Since every attempt
/// produced the identical symptom regardless of what changed later in the
/// method, the field lookup below may simply be failing (returning null)
/// every time, silently skipping everything after it. Each step that used
/// to continue silently on failure now throws a specific exception
/// instead, so BimFlowCommand's error dialog reports exactly which step
/// broke instead of masking it behind a generic downstream "no ElementId
/// column" message from the import side.
/// </summary>
public static class ScheduleExportHelper
{
    private const string ElementIdHeading = "ElementId";

    public static void ExportWithElementId(Document doc, ViewSchedule schedule, string folder, string fileName, ViewScheduleExportOptions options)
    {
        var definition = schedule.Definition;
        var idParameterId = new ElementId(BuiltInParameter.ID_PARAM);

        using var group = new TransactionGroup(doc, "BIMFlow: Temporary Element ID column for export");
        group.Start();
        try
        {
            using (var transaction = new Transaction(doc, "Add Element ID column"))
            {
                transaction.Start();

                var field = Enumerable.Range(0, definition.GetFieldCount())
                    .Select(definition.GetField)
                    .FirstOrDefault(f => f.HasSchedulableField && f.ParameterId == idParameterId);

                if (field is null)
                {
                    var idField = definition.GetSchedulableFields().FirstOrDefault(f => f.ParameterId == idParameterId);
                    if (idField is null)
                        throw new InvalidOperationException(
                            "BIMFlow diagnostic: this schedule's category doesn't expose an Element ID schedulable field " +
                            "(GetSchedulableFields() had none with ParameterId == BuiltInParameter.ID_PARAM).");

                    field = definition.AddField(idField);
                    if (field is null)
                        throw new InvalidOperationException("BIMFlow diagnostic: ScheduleDefinition.AddField returned null for the Element ID field.");
                }

                // Force our own literal heading, overwriting whatever Revit's
                // own default (or a pre-existing custom rename) says — the
                // import side matches on this exact string.
                field.ColumnHeading = ElementIdHeading;
                if (field.ColumnHeading != ElementIdHeading)
                    throw new InvalidOperationException(
                        $"BIMFlow diagnostic: ColumnHeading didn't stick — Revit reports it as \"{field.ColumnHeading}\" " +
                        $"right after setting it to \"{ElementIdHeading}\".");

                transaction.Commit();
            }

            schedule.Export(folder, fileName, options);
        }
        finally
        {
            group.RollBack();
        }
    }
}
