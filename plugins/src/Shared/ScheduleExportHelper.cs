using Autodesk.Revit.DB;

namespace BIMFlow.Shared;

/// <summary>
/// Exports a schedule to CSV the same way ViewSchedule.Export always has,
/// but first guarantees an Element ID column with a known, literal
/// "ElementId" heading is present — added temporarily if the schedule
/// doesn't already have that field, and its heading force-set either way,
/// via a transaction that's rolled back immediately after export, so the
/// schedule's own field list/headings are never permanently changed.
///
/// Two real-world problems this works around, both confirmed against a
/// real project: (1) Revit's native schedule export only ever includes
/// whatever fields the schedule already displays, and none of this
/// project's schedules had an Element ID field, so Excel2Revit's import
/// mode (which matches edited rows back to elements by an ElementId
/// column) had nothing to match on. (2) Even after adding the field,
/// Revit's own default column heading for it isn't the literal string
/// "ElementId" BrandedXlsx.ReadTable's anchor match looks for — it's
/// whatever Revit itself calls that field, which is locale-dependent
/// (this project's Revit UI is German) and not something to rely on
/// staying constant. Forcing ColumnHeading ourselves sidesteps both the
/// localization and the "what does Revit call this today" guesswork.
/// </summary>
public static class ScheduleExportHelper
{
    private const string ElementIdHeading = "ElementId";

    public static void ExportWithElementId(Document doc, ViewSchedule schedule, string folder, string fileName, ViewScheduleExportOptions options)
    {
        var definition = schedule.Definition;
        var idParameterId = new ElementId(BuiltInParameter.ID_PARAM);

        using var transaction = new Transaction(doc, "BIMFlow: Temporary Element ID column for export");
        transaction.Start();
        try
        {
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

            // Schedules recompute their cell text lazily. Without forcing a
            // regenerate here, Export() can still serialize the schedule's
            // pre-change cached body — the field/heading edits above are
            // visible to the API but not yet baked into the exported text.
            // Confirmed as the real cause of "ElementId" never showing up in
            // the export even though ColumnHeading was set successfully.
            doc.Regenerate();

            schedule.Export(folder, fileName, options);
        }
        finally
        {
            transaction.RollBack();
        }
    }
}
