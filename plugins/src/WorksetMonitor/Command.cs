using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.WorksetMonitor;

/// <summary>
/// Shows element counts per user workset. Per-workset "who owns what" and
/// "last synced when" aren't exposed as simple aggregate APIs — ownership
/// is tracked per-element (WorksharingUtils.GetWorksharingTooltipInfo) and
/// isn't something the public API rolls up per workset — so this focuses
/// on the one metric that's reliably available: how big each workset is.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "worksetmonitor";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        if (!doc.IsWorkshared)
        {
            TaskDialog.Show("BIMFlow — WorksetMonitor", "This project isn't workshared.");
            return Result.Succeeded;
        }

        var worksets = new FilteredWorksetCollector(doc)
            .OfKind(WorksetKind.UserWorkset)
            .ToWorksets()
            .OrderBy(w => w.Name)
            .ToList();

        var rows = new List<ResultRow>();

        foreach (var workset in worksets)
        {
            var count = new FilteredElementCollector(doc)
                .WherePasses(new ElementWorksetFilter(workset.Id))
                .WhereElementIsNotElementType()
                .GetElementCount();

            rows.Add(new ResultRow(
                new[] { workset.Name, count.ToString(), workset.IsOpen ? "Open" : "Closed", workset.IsVisibleByDefault ? "Visible" : "Hidden" },
                new List<ElementId>()));
        }

        var results = new ResultsListForm(
            "BIMFlow — WorksetMonitor",
            $"{worksets.Count} user workset(s), {rows.Sum(r => int.Parse(r.Cells[1]))} elements total.",
            new[] { "Workset", "Elements", "State", "Default Visibility" },
            rows,
            actionButtonText: "Close");

        results.ShowDialog();
        return Result.Succeeded;
    }
}
