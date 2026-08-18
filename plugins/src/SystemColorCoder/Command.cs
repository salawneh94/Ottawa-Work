using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.SystemColorCoder;

/// <summary>
/// Colors every duct/pipe/cable tray/conduit element in the active view by
/// its System Name, using direct per-element graphic overrides rather than
/// a saved view filter — simpler and just as visually effective for a
/// working view, though it won't show up as a reusable filter in the V/G
/// dialog the way a ParameterFilterElement-based version would.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "systemcolorcoder";

    private static readonly BuiltInCategory[] Categories =
    {
        BuiltInCategory.OST_DuctCurves,
        BuiltInCategory.OST_PipeCurves,
        BuiltInCategory.OST_CableTray,
        BuiltInCategory.OST_Conduit,
    };

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var elements = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .WherePasses(new ElementMulticategoryFilter(Categories))
            .ToList();

        if (elements.Count == 0)
        {
            TaskDialog.Show("BIMFlow — SystemColorCoder", "No MEP system elements are visible in the active view.");
            return Result.Succeeded;
        }

        var systemNames = elements
            .Select(e => e.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        if (systemNames.Count == 0)
        {
            TaskDialog.Show("BIMFlow — SystemColorCoder", "None of the visible elements have a system assigned yet.");
            return Result.Succeeded;
        }

        var colorBySystem = systemNames
            .Select((name, i) => (name, color: ColorPalette.ForIndex(i)))
            .ToDictionary(t => t.name!, t => t.color);

        var applied = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Color-Code Systems");
        transaction.Start();
        try
        {
            foreach (var element in elements)
            {
                var systemName = element.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString();
                if (string.IsNullOrWhiteSpace(systemName) || !colorBySystem.TryGetValue(systemName, out var color))
                    continue;

                var overrides = new OverrideGraphicSettings()
                    .SetProjectionLineColor(color)
                    .SetCutLineColor(color);

                view.SetElementOverrides(element.Id, overrides);
                applied++;
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show(
            "BIMFlow — SystemColorCoder",
            $"Color-coded {applied} element(s) across {systemNames.Count} system(s) in the active view.");

        return Result.Succeeded;
    }
}
