using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.PlansPerRoom;

/// <summary>
/// Opens the Plans Per Room dialog (naming/numbering templates, which view
/// types to build, a filterable room list, and per-room finish parameters),
/// then generates one sheet per checked room via RoomPlanGenerator.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "plansperroom";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var rooms = RoomPlanGenerator.CollectRooms(doc);
        if (rooms.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Plans Per Room", "No rooms were found in this project.");
            return Result.Succeeded;
        }

        var window = new RoomPlansWindow(doc, rooms);
        window.ShowDialog();
        if (!window.Generated) return Result.Cancelled;

        RoomPlanResult result;
        using var transaction = new Transaction(doc, "BIMFlow: Generate Plans Per Room");
        transaction.Start();
        try
        {
            result = RoomPlanGenerator.Generate(doc, window.SelectedRooms, window.ViewTypes, window.Output);
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        var summary = $"Created {result.SheetsCreated} sheet(s) and {result.ViewsCreated} view(s).";
        if (result.Skipped > 0) summary += $"\nSkipped {result.Skipped} room(s) without usable geometry.";
        if (result.Warnings.Count > 0) summary += "\n\n" + string.Join("\n", result.Warnings.Distinct());

        TaskDialog.Show("BIMFlow — Plans Per Room", summary);
        return Result.Succeeded;
    }
}
