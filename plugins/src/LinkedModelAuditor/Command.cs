using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.LinkedModelAuditor;

/// <summary>
/// Lists every linked model with its load/pin status and workset. Comparing
/// each link's last-saved time against "now" to flag staleness needs the
/// linked file's external reference metadata, which isn't uniformly
/// available for every link type (local vs. cloud-hosted) — this ships the
/// status/pin/workset report, which is reliable across all of them.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "linkedmodelauditor";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var linkInstances = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .ToList();

        if (linkInstances.Count == 0)
        {
            TaskDialog.Show("BIMFlow — LinkedModelAuditor", "No linked models are in this project.");
            return Result.Succeeded;
        }

        var worksetTable = doc.IsWorkshared ? doc.GetWorksetTable() : null;
        var rows = new List<ResultRow>();

        foreach (var instance in linkInstances)
        {
            var linkType = doc.GetElement(instance.GetTypeId()) as RevitLinkType;
            var status = linkType?.GetLinkedFileStatus().ToString() ?? "Unknown";
            var pinned = instance.Pinned ? "Pinned" : "Unpinned";

            var worksetName = "N/A";
            if (worksetTable is not null && instance.WorksetId != WorksetId.InvalidWorksetId)
            {
                var workset = worksetTable.GetWorkset(instance.WorksetId);
                worksetName = workset?.Name ?? "N/A";
            }

            rows.Add(new ResultRow(
                new[] { instance.Name, status, pinned, worksetName },
                new List<ElementId> { instance.Id }));
        }

        var results = new ResultsListForm(
            "BIMFlow — LinkedModelAuditor Results",
            $"{linkInstances.Count} linked model instance(s).",
            new[] { "Link Name", "Status", "Pin State", "Workset" },
            rows,
            actionButtonText: "Select in model");

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }
}
