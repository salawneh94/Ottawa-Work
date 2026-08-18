using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.GridRenumber;

/// <summary>
/// Batch-renames grids or levels using the same find/replace/prefix/suffix
/// rule as ViewRenamer. Scope: current selection if it contains any grids
/// or levels, otherwise every grid and level in the model.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "gridrenumber";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var selected = uiDoc.Selection.GetElementIds()
            .Select(id => doc.GetElement(id))
            .Where(e => e is Grid or Level)
            .ToList();

        var elements = selected.Count > 0
            ? selected
            : new FilteredElementCollector(doc)
                .WherePasses(new LogicalOrFilter(
                    new ElementClassFilter(typeof(Grid)),
                    new ElementClassFilter(typeof(Level))))
                .WhereElementIsNotElementType()
                .ToList();

        if (elements.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Grid Renumber", "No grids or levels found in the current selection or model.");
            return Result.Succeeded;
        }

        var window = new ElementRenamerForm("BIMFlow — Grid Renumber", elements);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        var renamePlan = window.BuildRenamePlan();
        if (renamePlan.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Grid Renumber", "No grids or levels matched the rename rule.");
            return Result.Succeeded;
        }

        using var transaction = new Transaction(doc, "BIMFlow: Renumber Grids/Levels");
        transaction.Start();
        try
        {
            TwoPassRenamer.Apply(
                renamePlan.Select(p => ((Action<string>)(name => p.Element.Name = name), p.NewName)).ToList());
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — Grid Renumber", $"Renamed {renamePlan.Count} element(s).");
        return Result.Succeeded;
    }
}
