using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.TagAll;

/// <summary>
/// Tags every untagged element of the chosen categories in the active
/// view. Placement is the element's bounding-box center with a small
/// per-tag offset to reduce exact overlaps when several elements sit close
/// together — a simple deterministic nudge, not full leader-routing
/// collision avoidance.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "tagallplus";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var window = new TagAllWindow();
        if (window.ShowDialog() != true)
            return Result.Cancelled;

        if (window.SelectedCategories.Count == 0)
            return Result.Succeeded;

        var alreadyTagged = new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(IndependentTag))
            .Cast<IndependentTag>()
            .SelectMany(t => t.GetTaggedLocalElementIds())
            .ToHashSet();

        var categories = window.SelectedCategories.Cast<int>().ToHashSet();

        var candidates = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .Where(e => e.Category is not null && categories.Contains((int)e.Category.Id.Value))
            .Where(e => !alreadyTagged.Contains(e.Id))
            .ToList();

        if (candidates.Count == 0)
        {
            TaskDialog.Show("BIMFlow — TagAll+", "Nothing to tag — every matching element already has a tag in this view.");
            return Result.Succeeded;
        }

        var tagged = 0;
        var index = 0;

        using var transaction = new Transaction(doc, "BIMFlow: Tag All");
        transaction.Start();
        try
        {
            foreach (var element in candidates)
            {
                var box = element.get_BoundingBox(view);
                if (box is null) continue;

                var center = (box.Min + box.Max) * 0.5;
                var nudge = new XYZ((index % 5) * 0.3, (index / 5 % 5) * 0.3, 0);
                var point = center + nudge;
                index++;

                try
                {
                    IndependentTag.Create(
                        doc,
                        ElementId.InvalidElementId,
                        view.Id,
                        new Reference(element),
                        false,
                        TagOrientation.Horizontal,
                        point);
                    tagged++;
                }
                catch (Exception)
                {
                    // No tag family loaded for this category, or it can't be tagged in this view type — skip it.
                }
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.RollBack();
            throw;
        }

        TaskDialog.Show("BIMFlow — TagAll+", $"Tagged {tagged} of {candidates.Count} candidate element(s).");
        return Result.Succeeded;
    }
}
