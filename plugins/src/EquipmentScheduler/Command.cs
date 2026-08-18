using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.EquipmentScheduler;

/// <summary>
/// Generates a Mechanical Equipment schedule with a sensible default set of
/// fields. Linking in manufacturer cut-sheet data from an external
/// spreadsheet is a separate, larger feature (needs a matching key,
/// conflict handling, refresh-on-change) — this covers the "just give me a
/// starting schedule" need on its own.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "equipmentscheduler";

    private static readonly string[] PreferredFieldNames =
    {
        "Family and Type", "Mark", "Manufacturer", "Model", "Comments", "Count",
    };

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var categoryId = new ElementId(BuiltInCategory.OST_MechanicalEquipment);

        ViewSchedule schedule;
        try
        {
            schedule = ViewSchedule.CreateSchedule(doc, categoryId);
        }
        catch (Exception)
        {
            TaskDialog.Show("BIMFlow — EquipmentScheduler", "Couldn't create a schedule for Mechanical Equipment in this project.");
            return Result.Cancelled;
        }

        using var transaction = new Transaction(doc, "BIMFlow: Create Equipment Schedule");
        transaction.Start();
        try
        {
            schedule.Name = MakeUniqueName(doc, "Mechanical Equipment Schedule");

            var definition = schedule.Definition;
            var schedulable = definition.GetSchedulableFields();
            var added = 0;

            foreach (var fieldName in PreferredFieldNames)
            {
                var match = schedulable.FirstOrDefault(f => f.GetName(doc).Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                if (match is null) continue;

                definition.AddField(match);
                added++;
            }

            if (added == 0)
            {
                // Fall back to whatever fields the category actually exposes.
                foreach (var field in schedulable.Take(6))
                    definition.AddField(field);
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — EquipmentScheduler", $"Created \"{schedule.Name}\".");
        return Result.Succeeded;
    }

    private static string MakeUniqueName(Document doc, string baseName)
    {
        var existingNames = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Select(s => s.Name)
            .ToHashSet();

        if (!existingNames.Contains(baseName)) return baseName;

        var i = 2;
        while (existingNames.Contains($"{baseName} {i}")) i++;
        return $"{baseName} {i}";
    }
}
