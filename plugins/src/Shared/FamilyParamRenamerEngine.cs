// Explicit aliases, not "using System.IO;" — Shared has both UseWPF and UseWindowsForms, which drops
// System.IO from the implicit global usings.
using File = System.IO.File;
using Path = System.IO.Path;
using System.Text;

using Autodesk.Revit.DB;
// Shared has UseWindowsForms too, so the bare name "Binding" is ambiguous with System.Windows.Forms.Binding.
using RevitBinding = Autodesk.Revit.DB.Binding;
using RevitApplication = Autodesk.Revit.ApplicationServices.Application;

namespace OttawaWork.Shared;

public record SharedParamDefinition(string Name, Guid Guid, string Group, ForgeTypeId DataType);

/// <summary>How a shared parameter is bound in Manage → Project Parameters.</summary>
public record ProjectParamInfo(bool IsInstance, ISet<ElementId> CategoryIds, IReadOnlyList<string> CategoryNames);

/// <summary>One shared parameter found by a scan: every family that contains it, and its project
/// binding if it's also (or only) a project parameter.</summary>
public record FoundSharedParam(string Name, Guid Guid, ForgeTypeId DataType, SortedSet<string> FamilyNames)
{
    public ProjectParamInfo? Project { get; init; }
}

public record RenameFamily(ElementId FamilyId, string Name, string Category);

public enum MappingStatus { Ready, Skip, NoTarget, TypeMismatch, SameId }

public record FamilyRenameResult(string FamilyName, int Swapped, List<string> Messages, bool Failed);

public record ProjectParamResult(string OldName, string NewName, int Copied, bool Removed, List<string> Messages, bool Failed);

/// <summary>
/// Swaps shared parameters inside loaded families for different shared parameters (e.g. OMC_* → OI_*),
/// keeping each family's values, formulas and labels. Revit can't rename a shared parameter — its
/// identity is its GUID — and FamilyManager.ReplaceParameter only converts one direction per call
/// (shared → family via the string overload, family → shared via the ExternalDefinition overload), so
/// each swap is two calls through a temporary family parameter, wrapped in one SubTransaction so a
/// failed second step can't leave a stray temporary parameter behind.
/// </summary>
public static class FamilyParamRenamerEngine
{
    public static string DataTypeLabel(ForgeTypeId dataType)
    {
        try { return LabelUtils.GetLabelForSpec(dataType); }
        catch (Exception) { return dataType.TypeId; }
    }

    /// <summary>Every definition in a shared parameter file. SharedParametersFilename is a global,
    /// session-wide Revit setting, so the previous value is always restored.</summary>
    public static List<SharedParamDefinition> ReadDefinitions(RevitApplication app, string path)
    {
        var result = new List<SharedParamDefinition>();
        if (!File.Exists(path)) return result;

        var previous = app.SharedParametersFilename;
        try
        {
            app.SharedParametersFilename = path;
            var file = app.OpenSharedParameterFile();
            if (file is null) return result;
            foreach (var group in file.Groups)
                foreach (var definition in group.Definitions)
                    if (definition is ExternalDefinition external)
                        result.Add(new SharedParamDefinition(external.Name, external.GUID, group.Name, external.GetDataType()));
        }
        finally
        {
            app.SharedParametersFilename = previous;
        }
        return result.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<Family> EditableFamilies(Document doc) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(f => f.IsEditable && !f.IsInPlace)
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool HasPrefix(string name, string prefix) =>
        prefix.Length == 0 || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static void Record(Dictionary<Guid, FoundSharedParam> found, string name, Guid guid, ForgeTypeId dataType, string familyName)
    {
        if (!found.TryGetValue(guid, out var entry))
            found[guid] = entry = new FoundSharedParam(name, guid, dataType, new SortedSet<string>(StringComparer.OrdinalIgnoreCase));
        entry.FamilyNames.Add(familyName);
    }

    /// <summary>A shared parameter bound in Manage → Project Parameters. Key and Binding are the binding
    /// map's own objects — re-read after any change to the map, never reused across one.</summary>
    private sealed record SharedBinding(Definition Key, ElementBinding Binding, InternalDefinition Definition, Guid Guid);

    private static List<SharedBinding> SharedBindings(Document doc)
    {
        var result = new List<SharedBinding>();
        var iterator = doc.ParameterBindings.ForwardIterator();
        while (iterator.MoveNext())
        {
            if (iterator.Key is not InternalDefinition definition || iterator.Current is not ElementBinding binding) continue;
            if (doc.GetElement(definition.Id) is SharedParameterElement shared)
                result.Add(new SharedBinding(iterator.Key, binding, definition, shared.GuidValue));
        }
        return result;
    }

    private static ProjectParamInfo InfoOf(ElementBinding binding)
    {
        var categories = binding.Categories.Cast<Category>().ToList();
        return new ProjectParamInfo(
            binding is InstanceBinding,
            categories.Select(c => c.Id).ToHashSet(),
            categories.Select(c => c.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>Every shared parameter with the prefix that's bound as a project parameter. These never
    /// live inside a family, so swapping families can't reach them — RunProjectParameters replaces them.</summary>
    private static Dictionary<Guid, FoundSharedParam> ProjectParameters(Document doc, string prefix)
    {
        var result = new Dictionary<Guid, FoundSharedParam>();
        foreach (var bound in SharedBindings(doc))
        {
            if (!HasPrefix(bound.Definition.Name, prefix)) continue;
            result[bound.Guid] = new FoundSharedParam(bound.Definition.Name, bound.Guid, bound.Definition.GetDataType(), new SortedSet<string>(StringComparer.OrdinalIgnoreCase))
            {
                Project = InfoOf(bound.Binding),
            };
        }
        return result;
    }

    /// <summary>Fast scan without opening any family: reads shared parameters off each family's types
    /// and one placed instance per type. Instance parameters of families with no placed instance aren't
    /// visible this way — DeepScan opens each family to find those.
    ///
    /// Confirmed live (user-reported "Swapped 131 parameter(s) in 30 of 200 families"): a project
    /// parameter shows up on every element of its categories exactly like a family's own parameter, so
    /// the first version listed 170 families whose OMC values all came from Project Parameters — and the
    /// swap had nothing to change in them. A value that a project binding explains (same category, same
    /// instance/type) is now credited to the project parameter, not the family. A family that also
    /// contains that same parameter itself can't be told apart without opening it — DeepScan does.</summary>
    public static (List<RenameFamily> Families, List<FoundSharedParam> Params) QuickScan(Document doc, string prefix)
    {
        var instanceBySymbol = new Dictionary<ElementId, Element>();
        foreach (var instance in new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
            instanceBySymbol.TryAdd(instance.GetTypeId(), instance);

        var project = ProjectParameters(doc, prefix);
        var found = new Dictionary<Guid, FoundSharedParam>(project);
        var families = new List<RenameFamily>();
        foreach (var family in EditableFamilies(doc))
        {
            var hit = false;
            foreach (var symbolId in family.GetFamilySymbolIds())
            {
                var sources = new List<Element>();
                if (doc.GetElement(symbolId) is { } symbol) sources.Add(symbol);
                if (instanceBySymbol.TryGetValue(symbolId, out var instance)) sources.Add(instance);

                foreach (var source in sources)
                    foreach (Parameter parameter in source.Parameters)
                    {
                        if (!parameter.IsShared || !HasPrefix(parameter.Definition.Name, prefix)) continue;
                        if (project.TryGetValue(parameter.GUID, out var bound)
                            && bound.Project!.IsInstance == (source is not ElementType)
                            && source.Category is { } category
                            && bound.Project.CategoryIds.Contains(category.Id)) continue;
                        Record(found, parameter.Definition.Name, parameter.GUID, parameter.Definition.GetDataType(), family.Name);
                        hit = true;
                    }
            }
            if (hit) families.Add(new RenameFamily(family.Id, family.Name, family.FamilyCategory?.Name ?? ""));
        }
        return (families, found.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>Opens every editable family (read-only, closed without saving) and reads its
    /// FamilyManager — finds everything, including instance parameters of unplaced families, but is
    /// slow on a large project. Must be called with no open transaction on <paramref name="doc"/>.</summary>
    public static (List<RenameFamily> Families, List<FoundSharedParam> Params) DeepScan(Document doc, string prefix, Action<int, int>? progress = null)
    {
        var found = ProjectParameters(doc, prefix);
        var families = new List<RenameFamily>();
        var all = EditableFamilies(doc);
        for (var i = 0; i < all.Count; i++)
        {
            progress?.Invoke(i + 1, all.Count);
            var family = all[i];
            Document? familyDoc = null;
            try
            {
                familyDoc = doc.EditFamily(family);
                var hit = false;
                foreach (var parameter in familyDoc.FamilyManager.GetParameters())
                {
                    if (!parameter.IsShared || !HasPrefix(parameter.Definition.Name, prefix)) continue;
                    Record(found, parameter.Definition.Name, parameter.GUID, parameter.Definition.GetDataType(), family.Name);
                    hit = true;
                }
                if (hit) families.Add(new RenameFamily(family.Id, family.Name, family.FamilyCategory?.Name ?? ""));
            }
            catch (Exception) { /* a family that won't open is skipped, not fatal to the scan */ }
            finally { familyDoc?.Close(false); }
        }
        return (families, found.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>Same name with the old prefix swapped for the new one, matched case-insensitively.</summary>
    public static SharedParamDefinition? AutoMatch(string sourceName, string oldPrefix, string newPrefix, IReadOnlyList<SharedParamDefinition> definitions)
    {
        if (!HasPrefix(sourceName, oldPrefix)) return null;
        var candidate = newPrefix + sourceName.Substring(oldPrefix.Length);
        return definitions.FirstOrDefault(d => string.Equals(d.Name, candidate, StringComparison.OrdinalIgnoreCase));
    }

    public static bool SameDataType(ForgeTypeId a, ForgeTypeId b) => string.Equals(a.TypeId, b.TypeId, StringComparison.Ordinal);

    /// <summary>SameId: the target has the source's GUID — a renamed copy of the old entry in the file,
    /// which Revit treats as the same parameter (projects keep showing the old name), so it can't be
    /// used as a replacement.</summary>
    public static MappingStatus Evaluate(FoundSharedParam source, SharedParamDefinition? target) =>
        target is null ? MappingStatus.NoTarget
        : target.Guid == source.Guid ? MappingStatus.SameId
        : !SameDataType(target.DataType, source.DataType) ? MappingStatus.TypeMismatch
        : MappingStatus.Ready;

    /// <summary>Schedules and view filters that use any of the given shared parameters — they stay tied
    /// to the old GUIDs after a swap and show blank until re-pointed at the new parameters.</summary>
    public static List<string> AffectedViews(Document doc, IEnumerable<Guid> oldGuids)
    {
        var ids = oldGuids
            .Select(g => SharedParameterElement.Lookup(doc, g)?.Id)
            .Where(id => id is not null)
            .Cast<ElementId>()
            .ToHashSet();
        if (ids.Count == 0) return new List<string>();

        var result = new List<string>();
        foreach (var schedule in new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>())
        {
            if (schedule.IsTemplate) continue;
            try
            {
                var definition = schedule.Definition;
                for (var i = 0; i < definition.GetFieldCount(); i++)
                    if (ids.Contains(definition.GetField(i).ParameterId)) { result.Add($"Schedule: {schedule.Name}"); break; }
            }
            catch (Exception) { /* schedule shapes without a normal field list */ }
        }
        foreach (var filter in new FilteredElementCollector(doc).OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>())
        {
            try
            {
                if (filter.GetElementFilterParameters().Any(ids.Contains)) result.Add($"View filter: {filter.Name}");
            }
            catch (Exception) { }
        }
        return result;
    }

    /// <summary>Swaps every mapped shared parameter (old GUID → new GUID) in each family, then reloads
    /// it. Must be called with no open transaction on <paramref name="doc"/>: EditFamily and
    /// LoadFamily both throw if the project document is modifiable.</summary>
    public static List<FamilyRenameResult> Run(Document doc, IEnumerable<ElementId> familyIds, string sharedParameterFilePath, IReadOnlyDictionary<Guid, Guid> map)
    {
        var results = new List<FamilyRenameResult>();
        var app = doc.Application;
        var previous = app.SharedParametersFilename;
        try
        {
            app.SharedParametersFilename = sharedParameterFilePath;
            var definitions = OpenDefinitions(app, sharedParameterFilePath);

            foreach (var familyId in familyIds)
            {
                // Name read up front and every family isolated: reloading one family can replace other
                // families' managed objects (and its own), so nothing may touch a Family object after a
                // LoadFamily, and one failure must never stop the rest of the batch.
                string familyName;
                try { familyName = (doc.GetElement(familyId) as Family)?.Name ?? ""; }
                catch (Exception) { continue; }
                if (familyName.Length == 0) continue;

                try { results.Add(RunOne(doc, familyId, familyName, map, definitions)); }
                catch (Exception ex) { results.Add(new FamilyRenameResult(familyName, 0, new List<string> { $"Family not updated: {ex.Message}" }, true)); }
            }
        }
        finally
        {
            app.SharedParametersFilename = previous;
        }
        return results;
    }

    /// <summary>ExternalDefinitions by GUID from the shared parameter file Revit currently points at —
    /// callers switch SharedParametersFilename first and restore it after.</summary>
    private static Dictionary<Guid, ExternalDefinition> OpenDefinitions(RevitApplication app, string path)
    {
        var file = app.OpenSharedParameterFile()
            ?? throw new InvalidOperationException($"Couldn't open the shared parameter file:\n{path}");
        var definitions = new Dictionary<Guid, ExternalDefinition>();
        foreach (var group in file.Groups)
            foreach (var definition in group.Definitions)
                if (definition is ExternalDefinition external) definitions.TryAdd(external.GUID, external);
        return definitions;
    }

    /// <summary>Confirmed live (user-reported "The referenced object is not valid, possibly because it
    /// has been deleted"): the first version read Family.Name after LoadFamily, which can replace the
    /// family's managed object — and read it again in the catch, so the error escaped and stopped the whole
    /// batch after the first family. Everything here now works from the id/name captured before the edit,
    /// and each parameter is re-fetched by GUID right before its swap, since earlier ReplaceParameter calls
    /// in the same family can invalidate FamilyParameter objects gathered up front.</summary>
    private static FamilyRenameResult RunOne(Document doc, ElementId familyId, string familyName, IReadOnlyDictionary<Guid, Guid> map, Dictionary<Guid, ExternalDefinition> definitions)
    {
        var messages = new List<string>();
        var swapped = 0;
        Document? familyDoc = null;
        try
        {
            if (doc.GetElement(familyId) is not Family family)
                return new FamilyRenameResult(familyName, 0, new List<string> { "Family no longer in the project." }, true);

            familyDoc = doc.EditFamily(family);
            var manager = familyDoc.FamilyManager;
            var targetGuids = manager.GetParameters()
                .Where(p => p.IsShared && map.ContainsKey(p.GUID))
                .Select(p => p.GUID)
                .ToList();
            if (targetGuids.Count == 0)
                return new FamilyRenameResult(familyName, 0, new List<string> { "No mapped parameters in this family." }, false);

            using (var transaction = new Transaction(familyDoc, "Swap shared parameters"))
            {
                transaction.Start();
                var tempIndex = 0;
                foreach (var oldGuid in targetGuids)
                {
                    var oldName = oldGuid.ToString();
                    using var step = new SubTransaction(familyDoc);
                    step.Start();
                    try
                    {
                        var parameter = manager.get_Parameter(oldGuid);
                        if (parameter is null) { step.RollBack(); messages.Add($"{oldName}: no longer in the family — skipped."); continue; }
                        oldName = parameter.Definition.Name;

                        if (!definitions.TryGetValue(map[oldGuid], out var newDefinition))
                        {
                            step.RollBack();
                            messages.Add($"{oldName}: target not found in the shared parameter file — skipped.");
                            continue;
                        }
                        if (manager.get_Parameter(newDefinition.GUID) is not null || manager.get_Parameter(newDefinition.Name) is not null)
                        {
                            step.RollBack();
                            messages.Add($"{oldName}: family already has {newDefinition.Name} — skipped.");
                            continue;
                        }

                        var group = parameter.Definition is InternalDefinition internalDefinition ? internalDefinition.GetGroupTypeId() : new ForgeTypeId();
                        var isInstance = parameter.IsInstance;
                        string tempName;
                        do { tempName = $"OttawaSwapTemp_{tempIndex++}"; } while (manager.get_Parameter(tempName) is not null);

                        var temporary = manager.ReplaceParameter(parameter, tempName, group, isInstance);
                        manager.ReplaceParameter(temporary, newDefinition, group, isInstance);
                        step.Commit();
                        swapped++;
                        messages.Add($"{oldName} → {newDefinition.Name}");
                    }
                    catch (Exception ex)
                    {
                        if (step.HasStarted() && !step.HasEnded()) step.RollBack();
                        messages.Add($"{oldName}: {ex.Message}");
                    }
                }

                if (swapped == 0)
                {
                    transaction.RollBack();
                    return new FamilyRenameResult(familyName, 0, messages, false);
                }
                transaction.Commit();
            }

            familyDoc.LoadFamily(doc, new OverwriteLoadOptions());
            return new FamilyRenameResult(familyName, swapped, messages, false);
        }
        catch (Exception ex)
        {
            messages.Add($"Family not updated: {ex.Message}");
            return new FamilyRenameResult(familyName, 0, messages, true);
        }
        finally
        {
            try { familyDoc?.Close(false); }
            catch (Exception) { /* the edit copy is discarded either way */ }
        }
    }

    // ---- Project parameters ----
    //
    // A shared parameter bound in Manage → Project Parameters isn't inside any family, so the family swap
    // can't touch it. The project-level equivalent of the swap: bind the new parameter to the same
    // categories (same instance/type, group and vary-by-group setting), copy every element's value
    // across, then remove the old binding. All in one transaction, so it's a single Ctrl+Z.

    /// <summary>Replaces each mapped project parameter (old GUID → new GUID). Must be called with no open
    /// transaction on <paramref name="doc"/> — it opens its own.</summary>
    public static List<ProjectParamResult> RunProjectParameters(Document doc, string sharedParameterFilePath, IReadOnlyDictionary<Guid, Guid> map)
    {
        var results = new List<ProjectParamResult>();
        if (map.Count == 0) return results;

        var app = doc.Application;
        var previous = app.SharedParametersFilename;
        try
        {
            app.SharedParametersFilename = sharedParameterFilePath;
            var definitions = OpenDefinitions(app, sharedParameterFilePath);

            using var transaction = new Transaction(doc, "Replace project parameters");
            transaction.Start();
            foreach (var (oldGuid, newGuid) in map)
                results.Add(ReplaceProjectParameter(doc, oldGuid, newGuid, definitions));

            // Each parameter ran in its own SubTransaction, and a failed one already rolled itself back —
            // so committing keeps exactly the ones that worked.
            if (transaction.Commit() != TransactionStatus.Committed)
                return results
                    .Select(r => r with { Copied = 0, Removed = false, Failed = true, Messages = r.Messages.Append("Revit rolled the change back — nothing was changed.").ToList() })
                    .ToList();
        }
        finally
        {
            app.SharedParametersFilename = previous;
        }
        return results;
    }

    private static ProjectParamResult ReplaceProjectParameter(Document doc, Guid oldGuid, Guid newGuid, Dictionary<Guid, ExternalDefinition> definitions)
    {
        var messages = new List<string>();
        var old = SharedBindings(doc).FirstOrDefault(b => b.Guid == oldGuid);
        if (old is null)
            return new ProjectParamResult(oldGuid.ToString(), "", 0, false, new List<string> { "Not a project parameter in this model — nothing to do." }, false);
        var oldName = old.Definition.Name;
        if (!definitions.TryGetValue(newGuid, out var newDefinition))
            return new ProjectParamResult(oldName, "", 0, false, new List<string> { "New parameter not found in the shared parameter file — skipped." }, true);
        var newName = newDefinition.Name;

        // Everything needed from the old binding is read up front: any change to the binding map can
        // invalidate the Definition/Binding objects it handed out.
        var isInstance = old.Binding is InstanceBinding;
        var categoryIds = old.Binding.Categories.Cast<Category>().Select(c => c.Id).ToList();
        var group = old.Definition.GetGroupTypeId();
        var variesAcrossGroups = old.Definition.VariesAcrossGroups;

        using var step = new SubTransaction(doc);
        step.Start();
        try
        {
            var existing = SharedBindings(doc).FirstOrDefault(b => b.Guid == newGuid);
            var freshBinding = existing is null;
            if (existing is not null)
            {
                if ((existing.Binding is InstanceBinding) != isInstance)
                {
                    step.RollBack();
                    return new ProjectParamResult(oldName, newName, 0, false, new List<string>
                    {
                        $"{newName} is already a project parameter, but as a {(isInstance ? "type" : "instance")} parameter and {oldName} is {(isInstance ? "instance" : "type")} — Revit can't bind one parameter both ways. Skipped; change one of them in Project Parameters first.",
                    }, true);
                }
                var merged = doc.Application.Create.NewCategorySet();
                foreach (Category category in existing.Binding.Categories) merged.Insert(category);
                var added = 0;
                foreach (var id in categoryIds)
                    if (Category.GetCategory(doc, id) is { } category && !merged.Contains(category)) { merged.Insert(category); added++; }
                if (added > 0)
                {
                    if (!doc.ParameterBindings.ReInsert(existing.Key, NewBinding(doc, merged, isInstance), existing.Definition.GetGroupTypeId()))
                        throw new InvalidOperationException($"Revit refused to add {oldName}'s categories to {newName}.");
                    messages.Add($"{newName} was already a project parameter — added {added} of {oldName}'s categories to it.");
                }
            }
            else
            {
                if (AnyBindingNamed(doc, newName))
                {
                    step.RollBack();
                    return new ProjectParamResult(oldName, newName, 0, false, new List<string>
                    {
                        $"A different project parameter is already called {newName} — skipped, so the model doesn't end up with two parameters of the same name.",
                    }, true);
                }
                var categories = doc.Application.Create.NewCategorySet();
                foreach (var id in categoryIds)
                    if (Category.GetCategory(doc, id) is { } category) categories.Insert(category);
                if (categories.IsEmpty || !doc.ParameterBindings.Insert(newDefinition, NewBinding(doc, categories, isInstance), group))
                    throw new InvalidOperationException($"Revit refused to bind {newName}.");
                messages.Add($"Bound {newName} to {categoryIds.Count} categor{(categoryIds.Count == 1 ? "y" : "ies")} as {(isInstance ? "an instance" : "a type")} parameter.");
            }

            // Before copying: a value per group instance can only be written once the parameter is
            // allowed to vary between groups, the way the old one was.
            if (isInstance && variesAcrossGroups && SharedParameterElement.Lookup(doc, newGuid)?.GetDefinition() is { VariesAcrossGroups: false } newInternal)
            {
                try { newInternal.SetAllowVaryBetweenGroups(doc, true); }
                catch (Exception ex) { messages.Add($"Couldn't set \"values can vary by group instance\": {ex.Message}"); }
            }

            doc.Regenerate();
            var (copied, kept, failed) = CopyValues(doc, categoryIds, isInstance, oldGuid, newGuid, overwrite: freshBinding);
            messages.Add($"Copied {copied} value(s) from {oldName}.");
            if (kept > 0) messages.Add($"{kept} element(s) already had a different {newName} value — kept that value.");
            if (failed > 0) messages.Add($"{failed} value(s) couldn't be copied (read-only, or in a group that doesn't allow different values).");

            var removed = false;
            if (kept == 0 && failed == 0)
            {
                // Re-read: the Insert/ReInsert above can invalidate the old key object.
                var oldKey = SharedBindings(doc).FirstOrDefault(b => b.Guid == oldGuid)?.Key;
                removed = oldKey is not null && doc.ParameterBindings.Remove(oldKey);
                messages.Add(removed ? $"Removed {oldName} from Project Parameters." : $"Couldn't remove {oldName} from Project Parameters — remove it by hand.");
            }
            else
            {
                messages.Add($"{oldName} kept in Project Parameters so no value is lost — check the elements above, then remove it by hand.");
            }

            step.Commit();
            return new ProjectParamResult(oldName, newName, copied, removed, messages, false);
        }
        catch (Exception ex)
        {
            if (step.HasStarted() && !step.HasEnded()) step.RollBack();
            messages.Add($"Not replaced: {ex.Message}");
            return new ProjectParamResult(oldName, newName, 0, false, messages, true);
        }
    }

    private static RevitBinding NewBinding(Document doc, CategorySet categories, bool isInstance) =>
        isInstance ? doc.Application.Create.NewInstanceBinding(categories) : doc.Application.Create.NewTypeBinding(categories);

    private static bool AnyBindingNamed(Document doc, string name)
    {
        var iterator = doc.ParameterBindings.ForwardIterator();
        while (iterator.MoveNext())
            if (string.Equals(iterator.Key.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>Copies old → new on every element (or element type) of the categories. With
    /// <paramref name="overwrite"/> (the new parameter was just bound) the old value always wins — it's
    /// the value people saw until now, while the new parameter can only hold a family default. Without it
    /// (the new parameter was already in use), a different non-empty new value is kept and counted.</summary>
    private static (int Copied, int Kept, int Failed) CopyValues(Document doc, ICollection<ElementId> categoryIds, bool isInstance, Guid oldGuid, Guid newGuid, bool overwrite)
    {
        if (categoryIds.Count == 0) return (0, 0, 0);
        var collector = new FilteredElementCollector(doc).WherePasses(new ElementMulticategoryFilter(categoryIds));
        var elements = isInstance ? collector.WhereElementIsNotElementType() : collector.WhereElementIsElementType();

        int copied = 0, kept = 0, failed = 0;
        foreach (var element in elements)
        {
            var source = element.get_Parameter(oldGuid);
            if (source is null || !HasContent(source)) continue;
            var target = element.get_Parameter(newGuid);
            if (target is null) { failed++; continue; }
            if (SameValue(source, target)) continue;
            if (!overwrite && HasContent(target)) { kept++; continue; }
            if (target.IsReadOnly) { failed++; continue; }
            try
            {
                var ok = source.StorageType switch
                {
                    StorageType.String => target.Set(source.AsString() ?? ""),
                    StorageType.Integer => target.Set(source.AsInteger()),
                    StorageType.Double => target.Set(source.AsDouble()),
                    StorageType.ElementId => target.Set(source.AsElementId()),
                    _ => false,
                };
                if (ok) copied++; else failed++;
            }
            catch (Exception) { failed++; }
        }
        return (copied, kept, failed);
    }

    private static bool HasContent(Parameter parameter) =>
        parameter.HasValue && (parameter.StorageType != StorageType.String || !string.IsNullOrEmpty(parameter.AsString()));

    private static bool SameValue(Parameter a, Parameter b) =>
        a.StorageType == b.StorageType && a.HasValue == b.HasValue && a.StorageType switch
        {
            StorageType.String => string.Equals(a.AsString() ?? "", b.AsString() ?? "", StringComparison.Ordinal),
            StorageType.Integer => a.AsInteger() == b.AsInteger(),
            StorageType.Double => Math.Abs(a.AsDouble() - b.AsDouble()) < 1e-9,
            StorageType.ElementId => a.AsElementId() == b.AsElementId(),
            _ => true,
        };

    private class OverwriteLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }

    // ---- Shared parameter file IDs ----
    //
    // A shared parameter file made by find/replacing names in a copy of another file keeps every
    // parameter's GUID, so Revit sees "OI_X" as the same parameter as "OMC_X" — and Revit can't rename a
    // shared parameter, so the swap needs the new names to have their own GUIDs. The Revit API can't
    // change a definition's GUID, so this edits the file's text directly: only the GUID column of PARAM
    // lines, keeping the file's own encoding, line endings and every other column byte-for-byte.

    /// <summary>GUID → name for every PARAM line, read straight from the file's text.</summary>
    public static Dictionary<Guid, string> ReadParamNames(string path)
    {
        var (text, _) = ReadText(path);
        var result = new Dictionary<Guid, string>();
        var (guidColumn, nameColumn) = (1, 2);
        foreach (var rawLine in text.Split('\n'))
        {
            var columns = rawLine.TrimEnd('\r').Split('\t');
            if (columns[0] == "*PARAM") (guidColumn, nameColumn) = (Array.IndexOf(columns, "GUID"), Array.IndexOf(columns, "NAME"));
            if (columns[0] != "PARAM" || guidColumn < 0 || nameColumn < 0 || columns.Length <= Math.Max(guidColumn, nameColumn)) continue;
            if (Guid.TryParse(columns[guidColumn], out var guid)) result[guid] = columns[nameColumn];
        }
        return result;
    }

    /// <summary>GUIDs that appear in both files under different names — renamed copies.</summary>
    public static HashSet<Guid> RenamedCopies(string oldFilePath, string newFilePath)
    {
        var oldNames = ReadParamNames(oldFilePath);
        return ReadParamNames(newFilePath)
            .Where(entry => oldNames.TryGetValue(entry.Key, out var oldName) && !string.Equals(oldName, entry.Value, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .ToHashSet();
    }

    /// <summary>Gives every PARAM line whose GUID is in <paramref name="guids"/> a fresh GUID, after
    /// copying the file to a timestamped backup next to it. Returns old → new GUID for each change.</summary>
    public static (Dictionary<Guid, Guid> Changed, string BackupPath) RegenerateGuids(string path, ISet<Guid> guids)
    {
        var (text, encoding) = ReadText(path);
        var changed = new Dictionary<Guid, Guid>();
        var lines = text.Split('\n');
        var guidColumn = 1;
        for (var i = 0; i < lines.Length; i++)
        {
            var hasCarriageReturn = lines[i].EndsWith('\r');
            var columns = lines[i].TrimEnd('\r').Split('\t');
            if (columns[0] == "*PARAM") guidColumn = Array.IndexOf(columns, "GUID");
            if (columns[0] != "PARAM" || guidColumn < 0 || columns.Length <= guidColumn) continue;
            if (!Guid.TryParse(columns[guidColumn], out var oldGuid) || !guids.Contains(oldGuid)) continue;

            if (!changed.TryGetValue(oldGuid, out var newGuid)) changed[oldGuid] = newGuid = Guid.NewGuid();
            columns[guidColumn] = newGuid.ToString("D");
            lines[i] = string.Join('\t', columns) + (hasCarriageReturn ? "\r" : "");
        }

        if (changed.Count == 0) return (changed, "");

        var stem = Path.Combine(Path.GetDirectoryName(path) ?? "", $"{Path.GetFileNameWithoutExtension(path)}.backup-{DateTime.Now:yyyyMMdd-HHmmss}");
        var backupPath = stem + Path.GetExtension(path);
        for (var n = 2; File.Exists(backupPath); n++) backupPath = $"{stem}-{n}{Path.GetExtension(path)}";
        File.Copy(path, backupPath, overwrite: false);

        File.WriteAllText(path, string.Join('\n', lines), encoding);
        return (changed, backupPath);
    }

    /// <summary>Reads text in the file's own encoding so it can be written back unchanged: Revit writes
    /// shared parameter files as UTF-16 with a BOM, but hand-edited or tool-made ones are often UTF-8 or
    /// Windows-1252 — and German names (ä, ö, ü, ß) get corrupted if the wrong one is assumed.</summary>
    private static (string Text, Encoding Encoding) ReadText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), Encoding.Unicode);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), Encoding.BigEndianUnicode);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), new UTF8Encoding(true));

        try
        {
            return (new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes), new UTF8Encoding(false));
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var windows1252 = Encoding.GetEncoding(1252);
            return (windows1252.GetString(bytes), windows1252);
        }
    }

    /// <summary>Mapping files are plain "old name;new name" lines, so the same mapping can be reused
    /// across projects (Halle 1, 2, 3...) and edited in Excel or a text editor.</summary>
    public static void SaveMapping(string path, IEnumerable<(string OldName, string NewName)> pairs) =>
        File.WriteAllLines(path, new[] { "Old parameter;New parameter" }.Concat(pairs.Select(p => $"{p.OldName};{p.NewName}")), new System.Text.UTF8Encoding(true));

    public static Dictionary<string, string> LoadMapping(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(path).Skip(1))
        {
            var parts = line.Split(';', ',', '\t');
            if (parts.Length < 2) continue;
            var oldName = parts[0].Trim();
            var newName = parts[1].Trim();
            if (oldName.Length > 0 && newName.Length > 0) result[oldName] = newName;
        }
        return result;
    }
}
