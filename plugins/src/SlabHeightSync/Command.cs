using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.SlabHeightSync;

/// <summary>
/// Pick a level and a target offset, and every floor hosted on that level
/// gets its Height Offset From Level parameter set in one pass.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "slabheightsync";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var floors = new FilteredElementCollector(doc)
            .OfClass(typeof(Floor))
            .Cast<Floor>()
            .ToList();

        if (floors.Count == 0)
        {
            TaskDialog.Show("BIMFlow — SlabHeightSync", "No floors were found in this project.");
            return Result.Succeeded;
        }

        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .Where(l => floors.Any(f => f.LevelId == l.Id))
            .OrderBy(l => l.Elevation)
            .ToList();

        if (levels.Count == 0)
        {
            TaskDialog.Show("BIMFlow — SlabHeightSync", "None of the floors in this project are hosted directly on a level.");
            return Result.Succeeded;
        }

        var levelPicker = new SimplePickerDialog(
            "BIMFlow — SlabHeightSync",
            "Update floors hosted on which level?",
            levels.Select(l => l.Name).ToList());

        if (levelPicker.ShowDialog() != true || levelPicker.SelectedText is null)
            return Result.Cancelled;

        var level = levels.First(l => l.Name == levelPicker.SelectedText);

        var offsetText = TextInputDialog.Prompt(
            "BIMFlow — SlabHeightSync",
            "Target Height Offset From Level, in meters (negative lowers the slab):",
            "0");

        if (offsetText is null || !double.TryParse(offsetText, out var offsetMeters))
            return Result.Cancelled;

        var offsetFeet = UnitUtils.ConvertToInternalUnits(offsetMeters, UnitTypeId.Meters);

        var targetFloors = floors.Where(f => f.LevelId == level.Id).ToList();
        var updated = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Sync Slab Height");
        transaction.Start();
        try
        {
            foreach (var floor in targetFloors)
            {
                var param = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                if (param is null || param.IsReadOnly) continue;

                param.Set(offsetFeet);
                updated++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — SlabHeightSync", $"Set Height Offset From Level to {offsetMeters} m on {updated} floor(s) on \"{level.Name}\".");
        return Result.Succeeded;
    }
}
