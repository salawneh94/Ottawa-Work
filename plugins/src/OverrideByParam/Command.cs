using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;
// UseWPF+UseWindowsForms together drops System.IO from implicit global
// usings (see Shared/LicenseStore.cs) — needed here for Path.
using Path = System.IO.Path;

namespace BIMFlow.OverrideByParam;

/// <summary>
/// Colors elements of a chosen category in the active view by a chosen
/// parameter's value, applied as real persistent ParameterFilterElement-
/// based view filters (editable later in Visibility/Graphics, unlike a
/// one-off per-element OverrideGraphicSettings) — the general-purpose
/// version of color-by-value that isn't locked to MEP systems the way
/// SystemColorCoder is. Also exports the colored view as PNG, and can
/// clear the filters this tool created.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    private const string FilterPrefix = "BIMFlow: ";

    protected override string PluginSlug => "overridebyparam";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var window = new OverrideByParamWindow(doc, view);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        return window.ChosenAction switch
        {
            ColorCodeAction.Preview => Preview(doc, view, window),
            ColorCodeAction.ApplyFilters => ApplyFilters(doc, view, window),
            ColorCodeAction.ExportPng => ExportPng(doc, view),
            ColorCodeAction.ClearFilters => ClearFilters(doc, view),
            _ => Result.Succeeded,
        };
    }

    private static OverrideGraphicSettings BuildOverrides(Autodesk.Revit.DB.Color color, int transparency, ColorCodeMode mode)
    {
        var overrides = new OverrideGraphicSettings().SetProjectionLineColor(color).SetCutLineColor(color);
        if (mode == ColorCodeMode.ColorAndTransparency) overrides = overrides.SetSurfaceTransparency(transparency);
        return overrides;
    }

    private Result Preview(Document doc, View view, OverrideByParamWindow window)
    {
        if (window.SelectedCategory is null || window.SelectedParameterName is null)
            return Result.Succeeded;

        var category = window.SelectedCategory;
        var paramName = window.SelectedParameterName;

        var elements = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .OfCategoryId(category.Id)
            .ToList();

        using var transaction = new Transaction(doc, "BIMFlow: Preview Color Code");
        transaction.Start();
        try
        {
            if (window.WipeFirst)
                foreach (var element in elements)
                    view.SetElementOverrides(element.Id, new OverrideGraphicSettings());

            var applied = 0;
            foreach (var element in elements)
            {
                var value = element.LookupParameter(paramName)?.AsValueString();
                var key = string.IsNullOrWhiteSpace(value) ? OverrideByParamWindow.NoValueKey : value!;
                if (key == OverrideByParamWindow.NoValueKey) continue;
                if (!window.SelectedValues.Contains(key)) continue;

                var overrides = BuildOverrides(window.ColorByValue[key], window.Transparency, window.Mode);
                view.SetElementOverrides(element.Id, overrides);
                applied++;
            }

            transaction.Commit();
            TaskDialog.Show("BIMFlow — Color Code", $"Previewed {applied} element(s) directly in the active view. Nothing was saved as a filter — use \"Apply as filters\" to make it persistent, or re-run Preview/Clear to change it.");
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("BIMFlow — Color Code", $"Couldn't preview: {ex.Message}");
            return Result.Failed;
        }

        return Result.Succeeded;
    }

    private Result ApplyFilters(Document doc, View view, OverrideByParamWindow window)
    {
        if (window.SelectedCategory is null || window.SelectedParameterName is null)
            return Result.Succeeded;

        var category = window.SelectedCategory;
        var paramName = window.SelectedParameterName;

        var sample = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .OfCategoryId(category.Id)
            .Select(e => e.LookupParameter(paramName))
            .FirstOrDefault(p => p is not null);

        if (sample is null)
        {
            TaskDialog.Show("BIMFlow — Color Code", "Couldn't find that parameter on any element of this category in the active view.");
            return Result.Cancelled;
        }

        var namePrefix = $"{FilterPrefix}{category.Name}: {paramName} = ";

        using var transaction = new Transaction(doc, "BIMFlow: Apply Color Code Filters");
        transaction.Start();
        try
        {
            if (window.WipeFirst)
            {
                foreach (var filterId in view.GetFilters().ToList())
                {
                    if (doc.GetElement(filterId) is ParameterFilterElement pfe && pfe.Name.StartsWith(namePrefix, StringComparison.Ordinal))
                        view.RemoveFilter(filterId);
                }
            }

            var applied = 0;
            foreach (var value in window.SelectedValues.Where(v => v != OverrideByParamWindow.NoValueKey))
            {
                var filterName = namePrefix + value;
                var rule = ParameterFilterRuleFactory.CreateEqualsRule(sample.Id, value);
                var elementFilter = new ElementParameterFilter(rule);

                var existing = new FilteredElementCollector(doc)
                    .OfClass(typeof(ParameterFilterElement))
                    .Cast<ParameterFilterElement>()
                    .FirstOrDefault(f => f.Name == filterName);

                var pfe = existing;
                if (pfe is null)
                    pfe = ParameterFilterElement.Create(doc, filterName, new List<ElementId> { category.Id }, elementFilter);
                else
                    pfe.SetElementFilter(elementFilter);

                if (!view.GetFilters().Contains(pfe.Id))
                    view.AddFilter(pfe.Id);

                var overrides = BuildOverrides(window.ColorByValue[value], window.Transparency, window.Mode);
                view.SetFilterOverrides(pfe.Id, overrides);
                applied++;
            }

            transaction.Commit();
            TaskDialog.Show("BIMFlow — Color Code", $"Applied {applied} filter(s) for \"{paramName}\" on {category.Name} to the active view.");
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("BIMFlow — Color Code", $"Couldn't apply filters: {ex.Message}");
            return Result.Failed;
        }

        return Result.Succeeded;
    }

    private Result ExportPng(Document doc, View view)
    {
        using var saveDialog = new SaveFileDialog
        {
            Title = "Export view as PNG",
            Filter = "PNG image (*.png)|*.png",
            FileName = $"{view.Name}.png",
        };
        if (saveDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;

        var folder = Path.GetDirectoryName(saveDialog.FileName)!;
        var fileNameOnly = Path.GetFileNameWithoutExtension(saveDialog.FileName);

        var options = new ImageExportOptions
        {
            ExportRange = ExportRange.SetOfViews,
            FilePath = Path.Combine(folder, fileNameOnly),
            HLRandWFViewsFileType = ImageFileType.PNG,
            ShadowViewsFileType = ImageFileType.PNG,
            ZoomType = ZoomFitType.FitToPage,
            PixelSize = 2048,
            ImageResolution = ImageResolution.DPI_150,
        };
        options.SetViewsAndSheets(new List<ElementId> { view.Id });

        try
        {
            doc.ExportImage(options);
        }
        catch (Exception ex)
        {
            TaskDialog.Show("BIMFlow — Color Code", $"Export failed: {ex.Message}");
            return Result.Failed;
        }

        TaskDialog.Show("BIMFlow — Color Code", $"Exported the active view to:\n{folder}\n\n(Revit names the file after the view; check that folder if \"{fileNameOnly}.png\" isn't there exactly.)");
        return Result.Succeeded;
    }

    private Result ClearFilters(Document doc, View view)
    {
        using var transaction = new Transaction(doc, "BIMFlow: Clear Color Code Filters");
        transaction.Start();
        var removed = 0;
        try
        {
            foreach (var filterId in view.GetFilters().ToList())
            {
                if (doc.GetElement(filterId) is ParameterFilterElement pfe && pfe.Name.StartsWith(FilterPrefix, StringComparison.Ordinal))
                {
                    view.RemoveFilter(filterId);
                    removed++;
                }
            }
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            TaskDialog.Show("BIMFlow — Color Code", $"Couldn't clear filters: {ex.Message}");
            return Result.Failed;
        }

        TaskDialog.Show("BIMFlow — Color Code", $"Removed {removed} BIMFlow filter(s) from the active view.");
        return Result.Succeeded;
    }
}
