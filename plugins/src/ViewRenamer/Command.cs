using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.ViewRenamer;

[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "viewrenamer";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var views = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate)
            .OrderBy(v => v.Name)
            .Cast<Element>()
            .ToList();

        var window = new ElementRenamerForm("BIMFlow — View Renamer", views);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        var renamePlan = window.BuildRenamePlan();
        if (renamePlan.Count == 0)
        {
            TaskDialog.Show("BIMFlow — View Renamer", "No views matched the rename rule.");
            return Result.Succeeded;
        }

        using var transaction = new Transaction(doc, "BIMFlow: Rename Views");
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

        TaskDialog.Show("BIMFlow — View Renamer", $"Renamed {renamePlan.Count} view(s).");
        return Result.Succeeded;
    }
}
