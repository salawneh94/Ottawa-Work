using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

/// <summary>One revision as it appears on one sheet — Number is the sheet's own displayed number
/// (ViewSheet.GetRevisionNumberOnSheet), which can differ from Revision.RevisionNumber when the project
/// numbers revisions per sheet.</summary>
public record SheetRevisionEntry(string Number, bool Issued, string IssuedBy, string IssuedTo, string Date, string Description);

/// <summary>One sheet's issue status. EverIssued is true if ANY revision on the sheet is marked Issued,
/// which can differ from CurrentIssued (the latest revision may still be in progress after earlier
/// ones went out). CreatedBy/LastChangedBy only exist in a workshared model.</summary>
public record SheetTrackerRow(
    ElementId SheetId,
    string SheetNumber,
    string SheetName,
    string CurrentRevision,
    bool CurrentIssued,
    bool EverIssued,
    string IssuedBy,
    string IssuedTo,
    string IssueDate,
    List<SheetRevisionEntry> History,
    string CreatedBy,
    string LastChangedBy);

/// <summary>
/// Read-only scan behind Sheet Tracker: every real sheet in the project with its current revision,
/// whether that revision (or any earlier one) is marked Issued in Revit's Revisions table, and — in a
/// workshared model — who created and last changed it. Nothing here writes to the model.
/// </summary>
public static class SheetTrackerEngine
{
    public static List<SheetTrackerRow> Scan(Document doc)
    {
        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .Where(s => !s.IsPlaceholder)
            .OrderBy(s => s.SheetNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<SheetTrackerRow>();
        foreach (var sheet in sheets)
        {
            var history = new List<SheetRevisionEntry>();
            foreach (var revisionId in sheet.GetAllRevisionIds())
            {
                if (doc.GetElement(revisionId) is not Revision revision) continue;
                var number = SafeRevisionNumber(sheet, revisionId, revision);
                history.Add(new SheetRevisionEntry(number, revision.Issued, revision.IssuedBy ?? "", revision.IssuedTo ?? "", revision.RevisionDate ?? "", revision.Description ?? ""));
            }

            var current = doc.GetElement(sheet.GetCurrentRevision()) as Revision;
            var currentNumber = current is null ? "" : SafeRevisionNumber(sheet, current.Id, current);

            var (createdBy, lastChangedBy) = WorksharingInfo(doc, sheet.Id);

            rows.Add(new SheetTrackerRow(
                sheet.Id,
                sheet.SheetNumber,
                sheet.Name,
                currentNumber,
                current?.Issued ?? false,
                history.Any(h => h.Issued),
                current?.IssuedBy ?? "",
                current?.IssuedTo ?? "",
                current?.RevisionDate ?? "",
                history,
                createdBy,
                lastChangedBy));
        }
        return rows;
    }

    private static string SafeRevisionNumber(ViewSheet sheet, ElementId revisionId, Revision revision)
    {
        try { return sheet.GetRevisionNumberOnSheet(revisionId); }
        catch (Exception) { return revision.RevisionNumber ?? ""; }
    }

    private static (string CreatedBy, string LastChangedBy) WorksharingInfo(Document doc, ElementId id)
    {
        if (!doc.IsWorkshared) return ("", "");
        try
        {
            var info = WorksharingUtils.GetWorksharingTooltipInfo(doc, id);
            return (info.Creator ?? "", info.LastChangedBy ?? "");
        }
        catch (Exception) { return ("", ""); }
    }

    public static string Status(SheetTrackerRow row) =>
        row.CurrentIssued ? "Issued"
        : row.EverIssued ? "Previously issued"
        : row.History.Count > 0 ? "Revision not issued"
        : "Never issued";

    public static (List<string> Headers, List<List<string>> Rows) BuildExportTable(Document doc, IEnumerable<SheetTrackerRow> rows)
    {
        var headers = new List<string> { "Sheet Number", "Sheet Name", "Status", "Current Revision", "Issued By", "Issued To", "Issue Date", "All Revisions" };
        if (doc.IsWorkshared) headers.AddRange(new[] { "Created By", "Last Changed By" });

        var table = rows.Select(r =>
        {
            var history = string.Join("; ", r.History.Select(h => $"{h.Number}{(h.Issued ? " (issued" + (string.IsNullOrEmpty(h.Date) ? "" : " " + h.Date) + ")" : " (not issued)")}"));
            var line = new List<string> { r.SheetNumber, r.SheetName, Status(r), r.CurrentRevision, r.IssuedBy, r.IssuedTo, r.IssueDate, history };
            if (doc.IsWorkshared) line.AddRange(new[] { r.CreatedBy, r.LastChangedBy });
            return line;
        }).ToList();

        return (headers, table);
    }
}
