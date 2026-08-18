using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.DuplicateFinder;

/// <summary>
/// Finds elements of the same category and type sitting at (nearly) the same
/// location — the classic "double-pasted" duplicate — by clustering bounding
/// box centers within a tolerance, then lets the user review and delete them.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "duplicatefinder";

    private static readonly BuiltInCategory[] ScannedCategories =
    {
        BuiltInCategory.OST_Walls,
        BuiltInCategory.OST_Floors,
        BuiltInCategory.OST_Doors,
        BuiltInCategory.OST_Windows,
        BuiltInCategory.OST_Furniture,
        BuiltInCategory.OST_GenericModel,
        BuiltInCategory.OST_StructuralColumns,
        BuiltInCategory.OST_StructuralFraming,
    };

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        var groups = FindDuplicateGroups(doc);

        if (groups.Count == 0)
        {
            TaskDialog.Show("BIMFlow — Duplicate Finder", "No duplicate elements found in the scanned categories.");
            return Result.Succeeded;
        }

        var window = new DuplicateFinderWindow(groups);
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        var toDelete = window.SelectedElementIdsToDelete;
        if (toDelete.Count == 0)
            return Result.Succeeded;

        using var transaction = new Transaction(doc, "BIMFlow: Delete Duplicate Elements");
        transaction.Start();
        try
        {
            doc.Delete(toDelete);
            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — Duplicate Finder", $"Deleted {toDelete.Count} duplicate element(s).");
        return Result.Succeeded;
    }

    private static List<DuplicateGroup> FindDuplicateGroups(Document doc)
    {
        const double toleranceFeet = 0.1; // ~1.2"

        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .WherePasses(new ElementMulticategoryFilter(ScannedCategories))
            .ToList();

        var buckets = new Dictionary<(ElementId categoryId, ElementId typeId, long x, long y, long z), List<Element>>();

        foreach (var element in elements)
        {
            var box = element.get_BoundingBox(null);
            if (box is null) continue;

            var center = (box.Min + box.Max) * 0.5;
            var typeId = element.GetTypeId();
            var key = (
                element.Category?.Id ?? ElementId.InvalidElementId,
                typeId,
                (long)Math.Round(center.X / toleranceFeet),
                (long)Math.Round(center.Y / toleranceFeet),
                (long)Math.Round(center.Z / toleranceFeet));

            if (!buckets.TryGetValue(key, out var list))
                buckets[key] = list = new List<Element>();
            list.Add(element);
        }

        return buckets.Values
            .Where(list => list.Count > 1)
            .Select(list => new DuplicateGroup(list))
            .OrderByDescending(g => g.Elements.Count)
            .ToList();
    }
}

public class DuplicateGroup
{
    public DuplicateGroup(List<Element> elements) => Elements = elements;
    public List<Element> Elements { get; }
    public string CategoryName => Elements[0].Category?.Name ?? "Unknown";
    public string TypeName => Elements[0].Document.GetElement(Elements[0].GetTypeId())?.Name ?? "Unknown type";
}
