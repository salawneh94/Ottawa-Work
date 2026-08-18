using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.OverrideByParam;

/// <summary>
/// Colors every element of a chosen category in the active view by a
/// chosen parameter's value — the general-purpose version of color-by-value
/// that isn't locked to MEP systems the way SystemColorCoder is.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "overridebyparam";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var window = new OverrideByParamWindow(doc);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedCategory is null || window.SelectedParameterName is null)
            return Result.Succeeded;

        var elements = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .OfCategoryId(window.SelectedCategory.Id)
            .ToList();

        if (elements.Count == 0)
        {
            TaskDialog.Show("BIMFlow — OverrideByParam", "No matching elements are visible in the active view.");
            return Result.Succeeded;
        }

        var values = elements
            .Select(e => e.LookupParameter(window.SelectedParameterName)?.AsValueString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        if (values.Count == 0)
        {
            TaskDialog.Show("BIMFlow — OverrideByParam", "None of the visible elements have a value for that parameter.");
            return Result.Succeeded;
        }

        var colorByValue = values
            .Select((v, i) => (v, color: ColorPalette.ForIndex(i)))
            .ToDictionary(t => t.v!, t => t.color);

        var applied = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Override by Parameter");
        transaction.Start();
        try
        {
            foreach (var element in elements)
            {
                var value = element.LookupParameter(window.SelectedParameterName)?.AsValueString();
                if (string.IsNullOrWhiteSpace(value) || !colorByValue.TryGetValue(value, out var color)) continue;

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
            "BIMFlow — OverrideByParam",
            $"Color-coded {applied} element(s) across {values.Count} value(s) of \"{window.SelectedParameterName}\".");

        return Result.Succeeded;
    }
}
