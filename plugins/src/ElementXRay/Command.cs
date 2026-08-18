using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BIMFlow.Shared;
using System.Text;

namespace BIMFlow.ElementXRay;

/// <summary>
/// Diagnoses why a picked element isn't visible in the active view: category
/// visibility, direct element hide state, workset visibility, view
/// template, phase timing, view filters, crop region, and (on plan views)
/// view range. Category, direct-hide, and workset are safe to auto-fix in
/// place; template/phase/filter/crop/view-range are reported only, since
/// "fixing" those means changing a shared view or template setting rather
/// than something scoped to this one element.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "elementxray";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;
        var view = doc.ActiveView;

        var preselected = uiDoc.Selection.GetElementIds().FirstOrDefault();
        Element element;
        if (preselected is not null)
        {
            element = doc.GetElement(preselected);
        }
        else
        {
            var reference = uiDoc.Selection.PickObject(ObjectType.Element, "Pick the element to diagnose");
            element = doc.GetElement(reference);
        }

        var findings = new List<string>();
        var canAutoFixCategory = false;
        var canAutoFixDirectHide = false;
        var canAutoFixWorkset = false;
        var worksetToShow = WorksetId.InvalidWorksetId;

        var category = element.Category;
        if (category is not null)
        {
            var categoryHidden = view.GetCategoryHidden(category.Id);
            findings.Add(categoryHidden
                ? $"Category \"{category.Name}\" is hidden in this view."
                : $"Category \"{category.Name}\" is visible in this view.");
            canAutoFixCategory = categoryHidden;
        }

        var directlyHidden = element.IsHidden(view);
        findings.Add(directlyHidden ? "Element is directly hidden in this view (Hide Element)." : "Element is not directly hidden.");
        canAutoFixDirectHide = directlyHidden;

        if (doc.IsWorkshared)
        {
            var worksetId = element.WorksetId;
            var worksetVisibility = view.GetWorksetVisibility(worksetId);
            findings.Add(worksetVisibility == WorksetVisibility.Hidden
                ? $"Its workset is hidden in this view."
                : "Its workset is visible in this view.");
            if (worksetVisibility == WorksetVisibility.Hidden)
            {
                canAutoFixWorkset = true;
                worksetToShow = worksetId;
            }
        }

        if (view.ViewTemplateId != ElementId.InvalidElementId)
        {
            var template = doc.GetElement(view.ViewTemplateId) as View;
            findings.Add($"View template \"{template?.Name}\" is applied — it may be controlling category/filter visibility independently of this view's own settings.");
        }

        AddPhaseFindings(doc, view, element, findings);
        AddFilterFindings(doc, view, element, findings);
        AddCropFindings(view, element, findings);
        AddViewRangeFindings(view, element, findings);

        var canAutoFixAnything = canAutoFixCategory || canAutoFixDirectHide || canAutoFixWorkset;
        var body = new StringBuilder();
        body.AppendLine($"Diagnosing: {element.Category?.Name ?? element.GetType().Name} (Id {element.Id})");
        body.AppendLine();
        foreach (var f in findings) body.AppendLine("- " + f);

        if (canAutoFixAnything)
        {
            var fix = TaskDialog.Show(
                "BIMFlow — ElementX-Ray",
                body.ToString() + "\n\nFix the safe items above now?",
                TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

            if (fix == TaskDialogResult.Yes)
            {
                using var transaction = new Transaction(doc, "BIMFlow: ElementX-Ray Auto-Fix");
                transaction.Start();
                try
                {
                    if (canAutoFixCategory && category is not null) view.SetCategoryHidden(category.Id, false);
                    if (canAutoFixDirectHide) view.UnhideElements(new List<ElementId> { element.Id });
                    if (canAutoFixWorkset) view.SetWorksetVisibility(worksetToShow, WorksetVisibility.Visible);
                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.RollBack();
                    throw;
                }
                TaskDialog.Show("BIMFlow — ElementX-Ray", "Applied the safe fixes. Re-run to confirm the element is now visible.");
            }
        }
        else
        {
            TaskDialog.Show("BIMFlow — ElementX-Ray", body.ToString());
        }

        return Result.Succeeded;
    }

    private static void AddPhaseFindings(Document doc, View view, Element element, List<string> findings)
    {
        var viewPhaseParam = view.get_Parameter(BuiltInParameter.VIEW_PHASE);
        var createdParam = element.get_Parameter(BuiltInParameter.PHASE_CREATED);
        if (viewPhaseParam is null || createdParam is null) return;

        var viewPhaseId = viewPhaseParam.AsElementId();
        if (viewPhaseId == ElementId.InvalidElementId) return;

        var phases = doc.Phases.Cast<Phase>().ToList();
        var viewPhaseIndex = phases.FindIndex(p => p.Id == viewPhaseId);
        if (viewPhaseIndex < 0) return;

        var createdIndex = phases.FindIndex(p => p.Id == createdParam.AsElementId());
        if (createdIndex >= 0 && createdIndex > viewPhaseIndex)
            findings.Add("Element's Phase Created is later than this view's phase — it doesn't exist yet at this view's phase.");

        var demolishedParam = element.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED);
        var demolishedId = demolishedParam?.AsElementId() ?? ElementId.InvalidElementId;
        if (demolishedId != ElementId.InvalidElementId)
        {
            var demolishedIndex = phases.FindIndex(p => p.Id == demolishedId);
            if (demolishedIndex >= 0 && demolishedIndex <= viewPhaseIndex)
                findings.Add("Element's Phase Demolished is at or before this view's phase — it's demolished by this view's phase.");
        }
    }

    private static void AddFilterFindings(Document doc, View view, Element element, List<string> findings)
    {
        ICollection<ElementId> filterIds;
        try { filterIds = view.GetFilters(); }
        catch (Exception) { return; }

        foreach (var filterId in filterIds)
        {
            if (doc.GetElement(filterId) is not ParameterFilterElement pfe) continue;
            if (view.GetFilterVisibility(filterId)) continue; // filter isn't hiding anything

            try
            {
                var categories = pfe.GetCategories();
                if (element.Category is null || !categories.Contains(element.Category.Id)) continue;

                var ruleFilter = pfe.GetElementFilter();
                if (ruleFilter is not null && ruleFilter.PassesFilter(doc, element.Id))
                    findings.Add($"View filter \"{pfe.Name}\" matches this element and its visibility is turned off.");
            }
            catch (Exception)
            {
                // Filter rule evaluation can fail for parameters the element doesn't have — skip it.
            }
        }
    }

    private static void AddCropFindings(View view, Element element, List<string> findings)
    {
        if (!view.CropBoxActive) return;

        var elementBox = element.get_BoundingBox(view);
        if (elementBox is null) return;

        var cropBox = view.CropBox;
        var transform = cropBox.Transform;
        var corners = new[]
        {
            new XYZ(cropBox.Min.X, cropBox.Min.Y, cropBox.Min.Z), new XYZ(cropBox.Max.X, cropBox.Min.Y, cropBox.Min.Z),
            new XYZ(cropBox.Min.X, cropBox.Max.Y, cropBox.Min.Z), new XYZ(cropBox.Max.X, cropBox.Max.Y, cropBox.Min.Z),
            new XYZ(cropBox.Min.X, cropBox.Min.Y, cropBox.Max.Z), new XYZ(cropBox.Max.X, cropBox.Min.Y, cropBox.Max.Z),
            new XYZ(cropBox.Min.X, cropBox.Max.Y, cropBox.Max.Z), new XYZ(cropBox.Max.X, cropBox.Max.Y, cropBox.Max.Z),
        }.Select(transform.OfPoint).ToList();

        var worldMin = new XYZ(corners.Min(p => p.X), corners.Min(p => p.Y), corners.Min(p => p.Z));
        var worldMax = new XYZ(corners.Max(p => p.X), corners.Max(p => p.Y), corners.Max(p => p.Z));

        var overlaps = elementBox.Min.X <= worldMax.X && worldMin.X <= elementBox.Max.X
            && elementBox.Min.Y <= worldMax.Y && worldMin.Y <= elementBox.Max.Y
            && elementBox.Min.Z <= worldMax.Z && worldMin.Z <= elementBox.Max.Z;

        findings.Add(overlaps
            ? "Crop region is active and appears to include the element's location."
            : "Crop region is active and the element's location looks like it's outside it.");
    }

    private static void AddViewRangeFindings(View view, Element element, List<string> findings)
    {
        if (view is not ViewPlan viewPlan) return;

        var elementBox = element.get_BoundingBox(view);
        if (elementBox is null) return;

        try
        {
            var range = viewPlan.GetViewRange();
            var topLevel = viewPlan.Document.GetElement(range.GetLevelId(PlanViewPlane.TopClipPlane)) as Level;
            var bottomLevel = viewPlan.Document.GetElement(range.GetLevelId(PlanViewPlane.BottomClipPlane)) as Level;
            if (topLevel is null || bottomLevel is null) return;

            var topZ = topLevel.Elevation + range.GetOffset(PlanViewPlane.TopClipPlane);
            var bottomZ = bottomLevel.Elevation + range.GetOffset(PlanViewPlane.BottomClipPlane);

            var withinRange = elementBox.Max.Z >= bottomZ && elementBox.Min.Z <= topZ;
            findings.Add(withinRange
                ? "Element's elevation looks like it falls within this plan's view range."
                : "Element's elevation looks like it's outside this plan's view range (top/bottom clip planes).");
        }
        catch (Exception)
        {
            // View range read can fail on unusual plan configurations — skip this check rather than guess.
        }
    }
}
