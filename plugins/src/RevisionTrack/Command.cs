using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.RevisionTrack;

/// <summary>
/// Bulk-adds a revision (new or existing) to a chosen set of sheets'
/// revision tables, and sets who it was issued to/by across all of them in
/// one pass. Scoped to the revision-schedule side of revision tracking —
/// drawing revision clouds needs explicit sketch geometry per change and
/// isn't something this can safely generate automatically.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "revisiontrack";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var revisions = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Revisions)
            .Cast<Revision>()
            .OrderBy(r => r.SequenceNumber)
            .ToList();

        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .OrderBy(s => s.SheetNumber)
            .ToList();

        if (sheets.Count == 0)
        {
            TaskDialog.Show("BIMFlow — RevisionTrack", "No sheets were found in this project.");
            return Result.Succeeded;
        }

        var window = new RevisionTrackWindow(revisions, sheets);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedSheets.Count == 0)
            return Result.Succeeded;

        using var transaction = new Transaction(doc, "BIMFlow: Add Revision to Sheets");
        transaction.Start();
        try
        {
            var revision = window.SelectedRevision;
            if (revision is null)
            {
                revision = Revision.Create(doc);
                if (!string.IsNullOrWhiteSpace(window.NewRevisionDescription))
                    revision.Description = window.NewRevisionDescription;
            }

            if (!string.IsNullOrWhiteSpace(window.IssuedTo)) revision.IssuedTo = window.IssuedTo;
            if (!string.IsNullOrWhiteSpace(window.IssuedBy)) revision.IssuedBy = window.IssuedBy;

            foreach (var sheet in window.SelectedSheets)
            {
                var existing = sheet.GetAdditionalRevisionIds();
                if (existing.Contains(revision.Id)) continue;

                var updated = existing.ToList();
                updated.Add(revision.Id);
                sheet.SetAdditionalRevisionIds(updated);
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — RevisionTrack", $"Added the revision to {window.SelectedSheets.Count} sheet(s).");
        return Result.Succeeded;
    }
}
