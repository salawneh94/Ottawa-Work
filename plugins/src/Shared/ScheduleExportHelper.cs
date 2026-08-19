using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

/// <summary>
/// Exports a schedule to CSV the same way ViewSchedule.Export always has,
/// but first guarantees an Element ID column with a known, literal
/// "ElementId" heading is present — added temporarily if the schedule
/// doesn't already have that field, and its heading force-set either way.
///
/// Three real-world problems this works around, all confirmed against a
/// real project: (1) Revit's native schedule export only ever includes
/// whatever fields the schedule already displays, and none of this
/// project's schedules had an Element ID field, so Excel2Revit's import
/// mode (which matches edited rows back to elements by an ElementId
/// column) had nothing to match on. (2) Even after adding the field,
/// Revit's own default column heading for it isn't the literal string
/// "ElementId" BrandedXlsx.ReadTable's anchor match looks for — it's
/// whatever Revit itself calls that field, which is locale-dependent
/// (this project's Revit UI is German) and not something to rely on
/// staying constant. (3) The big one: Export() reads the schedule's
/// committed, regenerated state — a field added and a heading set inside
/// a transaction that's still open (even after Document.Regenerate())
/// never showed up in the exported file at all, confirmed by testing:
/// the export came back byte-identical to the schedule's original
/// columns. Export() simply doesn't see uncommitted transaction state.
///
/// So this commits the change for real (inside a TransactionGroup) so
/// Export() has something to see, then rolls the whole group back
/// afterward. TransactionGroup.RollBack() discards every transaction
/// assimilated into it as a single unit — the schedule's field list and
/// headings end up exactly as they started, with nothing left over in
/// the model or the undo stack.
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
                    if (idField is not null) field = definition.AddField(idField);
                }

                // Force our own literal heading, overwriting whatever Revit's
                // own default (or a pre-existing custom rename) says — the
                // import side matches on this exact string.
                if (field is not null) field.ColumnHeading = ElementIdHeading;

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
