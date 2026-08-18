using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.RoomColorFillPlan;

/// <summary>
/// The Rooms-specific version of OverrideByParam: pick a room parameter,
/// and every room in the active view gets a solid color fill keyed to that
/// parameter's value.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "roomcolorfillplan";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var rooms = new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(SpatialElement))
            .OfType<Room>()
            .Where(r => r.Area > 0)
            .ToList();

        if (rooms.Count == 0)
        {
            TaskDialog.Show("BIMFlow — RoomColorFillPlan", "No placed rooms are visible in the active view.");
            return Result.Succeeded;
        }

        var sample = rooms.First();
        var paramNames = sample.Parameters
            .Cast<Parameter>()
            .Select(p => p.Definition.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        var picker = new SimplePickerDialog("BIMFlow — RoomColorFillPlan", "Color-fill rooms by which parameter?", paramNames);
        if (picker.ShowDialog() != true || picker.SelectedText is null)
            return Result.Cancelled;

        var parameterName = picker.SelectedText;

        var values = rooms
            .Select(r => r.LookupParameter(parameterName)?.AsValueString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        if (values.Count == 0)
        {
            TaskDialog.Show("BIMFlow — RoomColorFillPlan", "None of the rooms have a value for that parameter.");
            return Result.Succeeded;
        }

        var solidFill = new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

        if (solidFill is null)
        {
            TaskDialog.Show("BIMFlow — RoomColorFillPlan", "No solid fill pattern is available in this project.");
            return Result.Cancelled;
        }

        var colorByValue = values
            .Select((v, i) => (v, color: ColorPalette.ForIndex(i)))
            .ToDictionary(t => t.v!, t => t.color);

        var applied = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Room Color Fill");
        transaction.Start();
        try
        {
            foreach (var room in rooms)
            {
                var value = room.LookupParameter(parameterName)?.AsValueString();
                if (string.IsNullOrWhiteSpace(value) || !colorByValue.TryGetValue(value, out var color)) continue;

                var overrides = new OverrideGraphicSettings()
                    .SetSurfaceForegroundPatternId(solidFill.Id)
                    .SetSurfaceForegroundPatternColor(color)
                    .SetSurfaceForegroundPatternVisible(true);

                view.SetElementOverrides(room.Id, overrides);
                applied++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        var legend = string.Join("\n", colorByValue.Keys.Select(v => $"  • {v}"));
        TaskDialog.Show(
            "BIMFlow — RoomColorFillPlan",
            $"Color-filled {applied} room(s) across {values.Count} value(s) of \"{parameterName}\":\n{legend}");

        return Result.Succeeded;
    }
}
