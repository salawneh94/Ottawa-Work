using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

public enum ModelCleanerCategory { InPlace, UnplacedViews, TemplatesAndFilters, Duplicates, RogueLinks, Materials, SheetsAndSchedules }

/// <summary>One row in a Model Cleaner tab. ElementIds is what Select/Delete/Blast Radius act on —
/// for In-Place it's the family AND its instances together (deleting the family removes the instances
/// with it, but selecting should show both), for Duplicates it's every type in that duplicate group.</summary>
public record ModelCleanerFinding(
    ModelCleanerCategory Category,
    string Name,
    string Category2,
    int InstanceCount,
    string Detail,
    List<ElementId> ElementIds);

/// <summary>
/// The scan side of Model Cleaner — seven independent audits, each reading
/// real model state and returning only what's actually flagged (same "only
/// show what needs attention" convention as OverriddenDimensionEngine).
/// Deliberately narrower than a full incremental purge in a couple of
/// places, called out at each scan: Rogue Links only covers RVT links
/// (CAD/DWG link status uses a different, unverified API surface this
/// build didn't have time to confirm against real RevitAPI.dll), and
/// Duplicates only covers Text Note Types (the best-defined, most common
/// real-world "duplicate type" cleanup case — comparing arbitrary type
/// categories for equivalence isn't a generic, well-defined problem).
/// </summary>
public static class ModelCleanerEngine
{
    public static List<ModelCleanerFinding> ScanAll(Document doc)
    {
        var findings = new List<ModelCleanerFinding>();
        findings.AddRange(ScanInPlace(doc));
        findings.AddRange(ScanUnplacedViews(doc));
        findings.AddRange(ScanTemplatesAndFilters(doc));
        findings.AddRange(ScanDuplicates(doc));
        findings.AddRange(ScanRogueLinks(doc));
        findings.AddRange(ScanMaterials(doc));
        findings.AddRange(ScanSheetsAndSchedules(doc));
        return findings;
    }

    /// <summary>In-place families — modeled directly in the project instead of loaded, can't be scheduled
    /// or reused across models, and hurt regen/open performance. One finding per family, carrying both the
    /// family element and every placed instance of its symbol(s) so Select/Delete act on the whole thing.</summary>
    public static List<ModelCleanerFinding> ScanInPlace(Document doc)
    {
        var inPlaceFamilies = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(f => f.IsInPlace)
            .ToList();

        var findings = new List<ModelCleanerFinding>();
        foreach (var family in inPlaceFamilies)
        {
            var symbolIds = family.GetFamilySymbolIds();
            var instances = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => symbolIds.Contains(fi.GetTypeId()))
                .ToList();

            var categoryName = family.FamilyCategory?.Name ?? "";
            var typeLabel = symbolIds.Count switch
            {
                0 => "",
                1 => (doc.GetElement(symbolIds.First()) as FamilySymbol)?.Name ?? "Default",
                _ => $"{symbolIds.Count} types",
            };

            var elementIds = new List<ElementId> { family.Id };
            elementIds.AddRange(instances.Select(i => i.Id));

            findings.Add(new ModelCleanerFinding(ModelCleanerCategory.InPlace, family.Name, categoryName, instances.Count, typeLabel, elementIds));
        }
        return findings;
    }

    /// <summary>Views never placed on a sheet — restricted to view types that can actually BE placed via a
    /// Viewport (schedules use ScheduleSheetInstance instead, handled separately by ScanSheetsAndSchedules;
    /// sheets/templates/browser/internal view types aren't "placeable" at all), so nothing here is a false
    /// positive from a view type that was never meant to go on a sheet.</summary>
    public static List<ModelCleanerFinding> ScanUnplacedViews(Document doc)
    {
        var placedViewIds = new FilteredElementCollector(doc)
            .OfClass(typeof(Viewport))
            .Cast<Viewport>()
            .Select(v => v.ViewId)
            .ToHashSet();

        var placeableTypes = new[]
        {
            ViewType.FloorPlan, ViewType.CeilingPlan, ViewType.Elevation, ViewType.Section,
            ViewType.ThreeD, ViewType.Detail, ViewType.DraftingView, ViewType.Legend,
            ViewType.EngineeringPlan, ViewType.AreaPlan,
        };

        var views = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate && placeableTypes.Contains(v.ViewType) && !placedViewIds.Contains(v.Id))
            .ToList();

        return views
            .Select(v => new ModelCleanerFinding(ModelCleanerCategory.UnplacedViews, v.Name, v.ViewType.ToString(), 0, "Not on any sheet", new List<ElementId> { v.Id }))
            .ToList();
    }

    /// <summary>View templates never assigned to any real view, and view filters (ParameterFilterElement)
    /// never added to any real view's filter list.</summary>
    public static List<ModelCleanerFinding> ScanTemplatesAndFilters(Document doc)
    {
        var findings = new List<ModelCleanerFinding>();

        var realViews = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate).ToList();

        var usedTemplateIds = realViews.Select(v => v.ViewTemplateId).Where(id => id != ElementId.InvalidElementId).ToHashSet();
        var templates = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => v.IsTemplate).ToList();
        foreach (var template in templates)
        {
            if (usedTemplateIds.Contains(template.Id)) continue;
            findings.Add(new ModelCleanerFinding(ModelCleanerCategory.TemplatesAndFilters, template.Name, "View Template", 0, "Not assigned to any view", new List<ElementId> { template.Id }));
        }

        var usedFilterIds = new HashSet<ElementId>();
        foreach (var view in realViews)
        {
            try { foreach (var filterId in view.GetFilters()) usedFilterIds.Add(filterId); }
            catch (Exception) { /* some view types (e.g. schedules) don't support filters at all */ }
        }
        var allFilters = new FilteredElementCollector(doc).OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>().ToList();
        foreach (var filter in allFilters)
        {
            if (usedFilterIds.Contains(filter.Id)) continue;
            findings.Add(new ModelCleanerFinding(ModelCleanerCategory.TemplatesAndFilters, filter.Name, "View Filter", 0, "Not added to any view", new List<ElementId> { filter.Id }));
        }

        return findings;
    }

    /// <summary>Text Note Types that render identically (same size, font, bold, italic) — the most common
    /// real-world "duplicate type" cleanup case, and the only one this scan covers; a generic "are these
    /// two types equivalent" check across arbitrary categories isn't a well-defined problem the way this
    /// one specific, common case is.</summary>
    public static List<ModelCleanerFinding> ScanDuplicates(Document doc)
    {
        var types = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().ToList();

        var findings = new List<ModelCleanerFinding>();
        var groups = types.GroupBy(TextTypeSignature).Where(g => g.Count() > 1);
        foreach (var group in groups)
        {
            var members = group.ToList();
            var names = string.Join(", ", members.Select(t => t.Name));
            findings.Add(new ModelCleanerFinding(ModelCleanerCategory.Duplicates, names, "Text Note Type", members.Count, "Identical size/font/bold/italic", members.Select(t => t.Id).ToList()));
        }
        return findings;
    }

    private static string TextTypeSignature(TextNoteType type)
    {
        var size = type.get_Parameter(BuiltInParameter.TEXT_SIZE)?.AsDouble() ?? 0;
        var font = type.get_Parameter(BuiltInParameter.TEXT_FONT)?.AsString() ?? "";
        var bold = type.get_Parameter(BuiltInParameter.TEXT_STYLE_BOLD)?.AsInteger() ?? 0;
        var italic = type.get_Parameter(BuiltInParameter.TEXT_STYLE_ITALIC)?.AsInteger() ?? 0;
        return $"{size:0.####}|{font}|{bold}|{italic}";
    }

    /// <summary>RVT links whose linked file can no longer be found or resolved. CAD/DWG link status isn't
    /// covered — CADLinkType exposes no direct status method the way RevitLinkType.GetLinkedFileStatus()
    /// does, and this build didn't have time to verify the ExternalFileReference-based path against real
    /// RevitAPI.dll, so it's left out rather than shipped unverified.</summary>
    public static List<ModelCleanerFinding> ScanRogueLinks(Document doc)
    {
        var findings = new List<ModelCleanerFinding>();
        var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>().ToList();
        foreach (var link in links)
        {
            LinkedFileStatus status;
            try { status = link.GetLinkedFileStatus(); }
            catch (Exception) { continue; }

            if (status is LinkedFileStatus.NotFound or LinkedFileStatus.Invalid)
                findings.Add(new ModelCleanerFinding(ModelCleanerCategory.RogueLinks, link.Name, "RVT Link", 0, status.ToString(), new List<ElementId> { link.Id }));
        }
        return findings;
    }

    /// <summary>Materials not referenced by any ElementId-storage parameter on any element or element type,
    /// and not applied as a paint material on any element (Element.GetMaterialIds(true)) — the same two
    /// signals Revit's own purge logic checks. A project-wide parameter scan, so this is the slowest of the
    /// seven scans on a large model; still a single manual pass, not something run automatically.</summary>
    public static List<ModelCleanerFinding> ScanMaterials(Document doc)
    {
        var materials = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().ToList();
        if (materials.Count == 0) return new List<ModelCleanerFinding>();
        var materialIds = materials.Select(m => m.Id).ToHashSet();
        var usedIds = new HashSet<ElementId>();

        void ScanParams(Element element)
        {
            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter.StorageType != StorageType.ElementId) continue;
                var id = parameter.AsElementId();
                if (id != ElementId.InvalidElementId && materialIds.Contains(id)) usedIds.Add(id);
            }
        }

        foreach (var element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
        {
            ScanParams(element);
            try { foreach (var id in element.GetMaterialIds(true)) if (materialIds.Contains(id)) usedIds.Add(id); }
            catch (Exception) { /* not every element type supports paint materials */ }
        }
        foreach (var element in new FilteredElementCollector(doc).WhereElementIsElementType())
            ScanParams(element);

        return materials
            .Where(m => !usedIds.Contains(m.Id))
            .Select(m => new ModelCleanerFinding(ModelCleanerCategory.Materials, m.Name, m.MaterialCategory, 0, "Not referenced anywhere in the model", new List<ElementId> { m.Id }))
            .ToList();
    }

    /// <summary>Blank sheets (no viewport and no schedule instance placed on them) and schedules never
    /// placed on any sheet — revision/internal keynote schedules excluded, same reasoning as ExcelSyncEngine:
    /// they're Revit's own bookkeeping views, not documentation a user manages.</summary>
    public static List<ModelCleanerFinding> ScanSheetsAndSchedules(Document doc)
    {
        var findings = new List<ModelCleanerFinding>();

        var sheetsWithViewports = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>().Select(v => v.SheetId).ToHashSet();
        var scheduleInstances = new FilteredElementCollector(doc).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>().ToList();
        var sheetsWithSchedules = scheduleInstances.Select(s => s.OwnerViewId).ToHashSet();
        var placedScheduleIds = scheduleInstances.Select(s => s.ScheduleId).ToHashSet();

        var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().ToList();
        foreach (var sheet in sheets)
        {
            if (sheetsWithViewports.Contains(sheet.Id) || sheetsWithSchedules.Contains(sheet.Id)) continue;
            findings.Add(new ModelCleanerFinding(ModelCleanerCategory.SheetsAndSchedules, $"{sheet.SheetNumber} - {sheet.Name}", "Blank Sheet", 0, "No views placed", new List<ElementId> { sheet.Id }));
        }

        var schedules = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule && !s.IsInternalKeynoteSchedule)
            .ToList();
        foreach (var schedule in schedules)
        {
            if (placedScheduleIds.Contains(schedule.Id)) continue;
            findings.Add(new ModelCleanerFinding(ModelCleanerCategory.SheetsAndSchedules, schedule.Name, "Unplaced Schedule", 0, "Not on any sheet", new List<ElementId> { schedule.Id }));
        }

        return findings;
    }

    /// <summary>Every element that depends on any of the given elements — the "blast radius" of deleting
    /// them. Element.GetDependentElements(null) returns every dependent regardless of type; the seed
    /// elements themselves are excluded from the result so the count reflects only what ELSE would go.</summary>
    public static List<ElementId> BlastRadius(Document doc, List<ElementId> seedIds)
    {
        var seedSet = seedIds.ToHashSet();
        var dependents = new HashSet<ElementId>();
        foreach (var id in seedIds)
        {
            if (doc.GetElement(id) is not { } element) continue;
            try
            {
                foreach (var dependentId in element.GetDependentElements(null))
                    if (!seedSet.Contains(dependentId)) dependents.Add(dependentId);
            }
            catch (Exception) { /* a handful of element types don't support dependent-element queries */ }
        }
        return dependents.ToList();
    }
}
