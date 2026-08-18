using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.MEPClashPrecheck;

/// <summary>
/// Flags MEP-vs-structural element pairs whose bounding boxes overlap, as a
/// fast in-Revit pre-check before a full coordination review. This is a
/// bounding-box heuristic, not true solid-geometry clash detection — solid
/// intersection across a whole model is expensive and most "boxes overlap"
/// pairs are real candidates worth a human look either way.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "mepclashprecheck";

    private static readonly BuiltInCategory[] MepCategories =
    {
        BuiltInCategory.OST_DuctCurves,
        BuiltInCategory.OST_PipeCurves,
        BuiltInCategory.OST_CableTray,
        BuiltInCategory.OST_Conduit,
    };

    private static readonly BuiltInCategory[] StructuralCategories =
    {
        BuiltInCategory.OST_StructuralFraming,
        BuiltInCategory.OST_StructuralColumns,
        BuiltInCategory.OST_Floors,
        BuiltInCategory.OST_Walls,
    };

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var mepElements = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .WherePasses(new ElementMulticategoryFilter(MepCategories))
            .ToList();

        var structuralElements = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .WherePasses(new ElementMulticategoryFilter(StructuralCategories))
            .ToList();

        if (mepElements.Count == 0 || structuralElements.Count == 0)
        {
            TaskDialog.Show("BIMFlow — MEPClashPrecheck", "Need both MEP and structural elements visible in the active view to check.");
            return Result.Succeeded;
        }

        var rows = new List<ResultRow>();

        foreach (var mep in mepElements)
        {
            var mepBox = mep.get_BoundingBox(view);
            if (mepBox is null) continue;

            foreach (var structural in structuralElements)
            {
                var structuralBox = structural.get_BoundingBox(view);
                if (structuralBox is null) continue;

                if (!BoundingBoxesOverlap(mepBox, structuralBox)) continue;

                rows.Add(new ResultRow(
                    new[]
                    {
                        mep.Category?.Name ?? "?",
                        $"Id {mep.Id.Value}",
                        structural.Category?.Name ?? "?",
                        $"Id {structural.Id.Value}",
                    },
                    new List<ElementId> { mep.Id, structural.Id }));
            }
        }

        if (rows.Count == 0)
        {
            TaskDialog.Show("BIMFlow — MEPClashPrecheck", $"Checked {mepElements.Count} MEP × {structuralElements.Count} structural element(s) — no bounding-box overlaps found.");
            return Result.Succeeded;
        }

        var results = new ResultsListForm(
            "BIMFlow — MEPClashPrecheck Results",
            $"{rows.Count} candidate clash(es) found (bounding-box overlap, not solid geometry).",
            new[] { "MEP Category", "MEP Id", "Structural Category", "Structural Id" },
            rows);

        if (results.ShowDialog() == true && results.ElementsToSelect.Count > 0)
            uiDoc.Selection.SetElementIds(results.ElementsToSelect);

        return Result.Succeeded;
    }

    private static bool BoundingBoxesOverlap(BoundingBoxXYZ a, BoundingBoxXYZ b)
    {
        return a.Min.X <= b.Max.X && b.Min.X <= a.Max.X
            && a.Min.Y <= b.Max.Y && b.Min.Y <= a.Max.Y
            && a.Min.Z <= b.Max.Z && b.Min.Z <= a.Max.Z;
    }
}
