using System.Text;
using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

/// <summary>
/// Exports a schedule to CSV the same way ViewSchedule.Export always has,
/// but first guarantees an Element ID column with a known, literal
/// "ElementId" heading is present — added temporarily if the schedule
/// doesn't already have that field, and its heading force-set either way.
///
/// Confirmed against a real project: matching a schedulable field by
/// ParameterId == new ElementId(BuiltInParameter.ID_PARAM) — the standard,
/// widely-documented way to add "Element ID" to a schedule via the API —
/// found nothing for this schedule's category. Rather than guess another
/// BuiltInParameter blind, this now dumps every schedulable field this
/// schedule's category actually offers (name, ParameterId, field type) into
/// the diagnostic exception when the ID_PARAM match fails, so the real
/// field to match on can be read directly off a real project instead of
/// guessed at from documentation.
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
                try
                {
                    var field = Enumerable.Range(0, definition.GetFieldCount())
                        .Select(definition.GetField)
                        .FirstOrDefault(f => f.HasSchedulableField && f.ParameterId == idParameterId);

                    if (field is null)
                    {
                        var schedulableFields = definition.GetSchedulableFields();
                        var idField = schedulableFields.FirstOrDefault(f => f.ParameterId == idParameterId);

                        if (idField is null)
                        {
                            // 1000+ fields is too many to scan by eye — narrow to
                            // ones whose name could plausibly be "Element ID"
                            // (German Revit keeps "Element" as a loanword, e.g.
                            // "Element-ID"), instead of dumping everything.
                            var named = schedulableFields.Select(sf =>
                            {
                                string name;
                                try { name = sf.GetName(doc); }
                                catch (Exception ex) { name = $"<GetName failed: {ex.Message}>"; }
                                return (Field: sf, Name: name);
                            }).ToList();

                            var candidates = named
                                .Where(t => t.Name.Contains("element", StringComparison.OrdinalIgnoreCase))
                                .Select(t => $"{t.Name} | ParameterId={t.Field.ParameterId.Value} | FieldType={t.Field.FieldType}")
                                .ToList();

                            throw new InvalidOperationException(
                                "BIMFlow diagnostic: no schedulable field matched BuiltInParameter.ID_PARAM " +
                                $"(value {idParameterId.Value}). {schedulableFields.Count} total fields exist; " +
                                $"{candidates.Count} contain \"element\":\n{string.Join("\n", candidates)}");
                        }

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
                catch
                {
                    if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                    throw;
                }
            }

            schedule.Export(folder, fileName, options);
        }
        finally
        {
            group.RollBack();
        }
    }
}
