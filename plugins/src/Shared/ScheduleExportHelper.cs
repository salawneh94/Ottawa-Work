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
                            // Neither BuiltInParameter.ID_PARAM nor a name
                            // containing "element" turned up a match — narrowing
                            // by guessed substrings has run out of road. Dump
                            // every field, unfiltered, to a text file next to
                            // the export so it can be searched directly instead
                            // of guessed at through more screenshot round trips.
                            var dumpPath = System.IO.Path.Combine(folder, "bimflow-schedulable-fields-diagnostic.txt");
                            var lines = schedulableFields.Select(sf =>
                            {
                                string name;
                                try { name = sf.GetName(doc); }
                                catch (Exception ex) { name = $"<GetName failed: {ex.Message}>"; }
                                return $"{name} | ParameterId={sf.ParameterId.Value} | FieldType={sf.FieldType}";
                            });
                            System.IO.File.WriteAllLines(dumpPath, lines);

                            throw new InvalidOperationException(
                                "BIMFlow diagnostic: no schedulable field matched BuiltInParameter.ID_PARAM " +
                                $"(value {idParameterId.Value}), and none of the {schedulableFields.Count} fields' " +
                                "names contain \"element\" either. Every field this schedule's category offers " +
                                $"(name, ParameterId, field type) has been written to:\n{dumpPath}\n\n" +
                                "Open that file and search it for whatever this schedule actually calls its own row identifier.");
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
