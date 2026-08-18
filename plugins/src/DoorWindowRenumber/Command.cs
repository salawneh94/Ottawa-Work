using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.DoorWindowRenumber;

[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "doorwindowrenumber";

    private static readonly BuiltInCategory[] DoorWindowCategories =
    {
        BuiltInCategory.OST_Doors,
        BuiltInCategory.OST_Windows,
    };

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var selected = uiDoc.Selection.GetElementIds()
            .Select(id => doc.GetElement(id))
            .Where(e => e.Category is not null
                        && DoorWindowCategories.Contains((BuiltInCategory)e.Category.Id.Value))
            .ToList();

        var categoryPickerNeeded = selected.Count == 0;

        var window = new DoorWindowRenumberWindow(selected.Count, categoryPickerNeeded);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        List<Element> elements;
        if (!categoryPickerNeeded)
        {
            elements = selected;
        }
        else
        {
            var category = window.UseWindowsCategory ? BuiltInCategory.OST_Windows : BuiltInCategory.OST_Doors;
            elements = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToList();
        }

        if (elements.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Door/Window Renumber", "No matching elements were found.");
            return Result.Succeeded;
        }

        var ordered = elements
            .Select(e => (Element: e, Point: (e.Location as LocationPoint)?.Point))
            .Where(t => t.Point is not null)
            .OrderBy(t => t.Element.LevelId.Value)
            .ThenByDescending(t => Math.Round(t.Point!.Y, 1))
            .ThenBy(t => Math.Round(t.Point!.X, 1))
            .Select(t => t.Element)
            .ToList();

        var renames = new List<(Action<string> setValue, string newValue)>();
        var number = window.StartNumber;
        foreach (var element in ordered)
        {
            var markParam = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
            if (markParam is null || markParam.IsReadOnly) continue;

            var value = $"{window.Prefix}{number}";
            renames.Add((v => markParam.Set(v), value));
            number += window.Increment;
        }

        using var transaction = new Transaction(doc, "BIMFlow: Renumber Doors/Windows");
        transaction.Start();
        try
        {
            TwoPassRenamer.Apply(renames);
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — Door/Window Renumber", $"Renumbered {renames.Count} element(s).");
        return Result.Succeeded;
    }
}
