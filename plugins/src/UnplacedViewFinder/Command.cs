using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.UnplacedViewFinder;

/// <summary>
/// Finds floor plan, section, elevation, 3D, drafting, and detail views
/// that aren't placed as a viewport on any sheet — the views most likely to
/// be scratch work left behind in the project browser. Schedules and
/// legends are intentionally excluded since they're often kept unplaced on
/// purpose. Reports and lets you select them; never deletes anything.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "unplacedviewfinder";

    private static readonly ViewType[] TargetViewTypes =
    {
        ViewType.FloorPlan, ViewType.EngineeringPlan, ViewType.AreaPlan, ViewType.CeilingPlan,
        ViewType.Elevation, ViewType.Section, ViewType.Detail, ViewType.ThreeD, ViewType.DraftingView,
    };

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var candidateViews = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate && TargetViewTypes.Contains(v.ViewType))
            .ToList();

        if (candidateViews.Count == 0)
        {
            TaskDialog.Show("BIMFlow — UnplacedViewFinder", "No eligible views were found in this project.");
            return Result.Succeeded;
        }

        var placedViewIds = new FilteredElementCollector(doc)
            .OfClass(typeof(Viewport))
            .Cast<Viewport>()
            .Select(vp => vp.ViewId)
            .ToHashSet();

        var unplaced = candidateViews
            .Where(v => !placedViewIds.Contains(v.Id))
            .OrderBy(v => v.ViewType.ToString())
            .ThenBy(v => v.Name)
            .ToList();

        if (unplaced.Count == 0)
        {
            TaskDialog.Show("BIMFlow — UnplacedViewFinder", $"Checked {candidateViews.Count} view(s). Every one is placed on a sheet.");
            return Result.Succeeded;
        }

        var rows = unplaced
            .Select(v => new ResultRow(
                new[] { v.Name, v.ViewType.ToString() },
                new List<ElementId> { v.Id }))
            .ToList();

        var results = new ResultsListForm(
            "BIMFlow — UnplacedViewFinder Results",
            $"{unplaced.Count} of {candidateViews.Count} view(s) aren't placed on any sheet.",
            new[] { "View", "Type" },
            rows,
            actionButtonText: "Select in model");

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }
}
