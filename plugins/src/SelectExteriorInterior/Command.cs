using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.SelectExteriorInterior;

/// <summary>
/// Pick Walls, Doors, or Windows and an Exterior/Interior filter — walls
/// are filtered by their own Function parameter, doors and windows by
/// their host wall's Function.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "selectexteriorinterior";

    private static readonly Dictionary<string, BuiltInCategory> Categories = new()
    {
        ["Walls"] = BuiltInCategory.OST_Walls,
        ["Doors"] = BuiltInCategory.OST_Doors,
        ["Windows"] = BuiltInCategory.OST_Windows,
    };

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var categoryPicker = new SimplePickerDialog(
            "BIMFlow — SelectExteriorInterior", "Select which category?", Categories.Keys.ToList());
        if (categoryPicker.ShowDialog() != true || categoryPicker.SelectedText is null)
            return Result.Cancelled;

        var categoryName = categoryPicker.SelectedText;

        var functionPicker = new SimplePickerDialog(
            "BIMFlow — SelectExteriorInterior", "Exterior or interior?", new List<string> { "Exterior", "Interior" });
        if (functionPicker.ShowDialog() != true || functionPicker.SelectedText is null)
            return Result.Cancelled;

        var targetFunction = functionPicker.SelectedText == "Exterior" ? WallFunction.Exterior : WallFunction.Interior;

        var elements = new FilteredElementCollector(doc, view.Id)
            .OfCategory(Categories[categoryName])
            .WhereElementIsNotElementType()
            .ToList();

        var matched = elements
            .Where(e => GetHostWallFunction(doc, e) == targetFunction)
            .Select(e => e.Id)
            .ToList();

        if (matched.Count == 0)
        {
            TaskDialog.Show("BIMFlow — SelectExteriorInterior", $"No {functionPicker.SelectedText.ToLower()} {categoryName.ToLower()} were found in the active view.");
            return Result.Succeeded;
        }

        uiDoc.Selection.SetElementIds(matched);
        TaskDialog.Show("BIMFlow — SelectExteriorInterior", $"Selected {matched.Count} {functionPicker.SelectedText.ToLower()} {categoryName.ToLower()}.");
        return Result.Succeeded;
    }

    private static WallFunction? GetHostWallFunction(Document doc, Element element)
    {
        var wall = element as Wall ?? (element as FamilyInstance)?.Host as Wall;
        var wallType = wall is null ? null : doc.GetElement(wall.GetTypeId()) as WallType;
        return wallType?.Function;
    }
}
