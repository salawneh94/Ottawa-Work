using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.InsulationManager;

/// <summary>
/// Applies a chosen insulation type and thickness to every selected pipe
/// or duct that doesn't already have insulation. Location-aware rules
/// (interior/exterior/plenum) would need a way to classify "location" that
/// isn't reliably derivable from the model alone — this covers the
/// type+thickness batch-apply, the part that's unambiguous.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "insulationmanager";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var selected = uiDoc.Selection.GetElementIds()
            .Select(id => doc.GetElement(id))
            .Where(e => e is Pipe or Duct)
            .ToList();

        if (selected.Count == 0)
        {
            TaskDialog.Show("BIMFlow — InsulationManager", "Select one or more pipes or ducts first, then run this tool.");
            return Result.Cancelled;
        }

        var types = new FilteredElementCollector(doc)
            .WhereElementIsElementType()
            .Where(e => e is PipeInsulationType or DuctInsulationType)
            .Cast<ElementType>()
            .OrderBy(t => t.Name)
            .ToList();

        if (types.Count == 0)
        {
            TaskDialog.Show("BIMFlow — InsulationManager", "No pipe or duct insulation types are loaded in this project.");
            return Result.Succeeded;
        }

        var window = new InsulationManagerWindow(types, selected.Count);
        if (window.ShowDialog() != true || window.SelectedType is null)
            return Result.Cancelled;

        var thicknessFeet = window.ThicknessInches / 12.0;
        var applied = 0;
        var skipped = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Apply Insulation");
        transaction.Start();
        try
        {
            foreach (var element in selected)
            {
                try
                {
                    if (element is Pipe pipe)
                    {
                        if (PipeInsulation.GetInsulationIds(doc, pipe.Id).Count > 0) { skipped++; continue; }
                        PipeInsulation.Create(doc, pipe.Id, window.SelectedType.Id, thicknessFeet);
                        applied++;
                    }
                    else if (element is Duct duct)
                    {
                        if (DuctInsulation.GetInsulationIds(doc, duct.Id).Count > 0) { skipped++; continue; }
                        DuctInsulation.Create(doc, duct.Id, window.SelectedType.Id, thicknessFeet);
                        applied++;
                    }
                }
                catch (Exception)
                {
                    skipped++;
                }
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "BIMFlow — InsulationManager",
            $"Insulated {applied} element(s). Skipped {skipped} (already insulated or incompatible).");

        return Result.Succeeded;
    }
}
