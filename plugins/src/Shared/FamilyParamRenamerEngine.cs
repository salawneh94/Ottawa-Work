// Explicit aliases, not "using System.IO;" — Shared has both UseWPF and UseWindowsForms, which drops
// System.IO from the implicit global usings.
using File = System.IO.File;

using Autodesk.Revit.DB;
using RevitApplication = Autodesk.Revit.ApplicationServices.Application;

namespace OttawaWork.Shared;

public record SharedParamDefinition(string Name, Guid Guid, string Group, ForgeTypeId DataType);

/// <summary>One shared parameter found in the scanned families, with every family it appears in.</summary>
public record FoundSharedParam(string Name, Guid Guid, ForgeTypeId DataType, SortedSet<string> FamilyNames);

public record RenameFamily(ElementId FamilyId, string Name, string Category);

public enum MappingStatus { Ready, Skip, NoTarget, TypeMismatch, SameId }

public record FamilyRenameResult(string FamilyName, int Swapped, List<string> Messages, bool Failed);

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

    /// <summary>Fast scan without opening any family: reads shared parameters off each family's types
    /// and one placed instance per type. Instance parameters of families with no placed instance aren't
    /// visible this way — DeepScan opens each family to find those.</summary>
    public static (List<RenameFamily> Families, List<FoundSharedParam> Params) QuickScan(Document doc, string prefix)
    {
        var instanceBySymbol = new Dictionary<ElementId, Element>();
        foreach (var instance in new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
            instanceBySymbol.TryAdd(instance.GetTypeId(), instance);

        var found = new Dictionary<Guid, FoundSharedParam>();
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
        var found = new Dictionary<Guid, FoundSharedParam>();
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
            var file = app.OpenSharedParameterFile()
                ?? throw new InvalidOperationException($"Couldn't open the shared parameter file:\n{sharedParameterFilePath}");

            var definitions = new Dictionary<Guid, ExternalDefinition>();
            foreach (var group in file.Groups)
                foreach (var definition in group.Definitions)
                    if (definition is ExternalDefinition external) definitions.TryAdd(external.GUID, external);

            foreach (var familyId in familyIds)
            {
                if (doc.GetElement(familyId) is not Family family) continue;
                results.Add(RunOne(doc, family, map, definitions));
            }
        }
        finally
        {
            app.SharedParametersFilename = previous;
        }
        return results;
    }

    private static FamilyRenameResult RunOne(Document doc, Family family, IReadOnlyDictionary<Guid, Guid> map, Dictionary<Guid, ExternalDefinition> definitions)
    {
        var messages = new List<string>();
        var swapped = 0;
        Document? familyDoc = null;
        try
        {
            familyDoc = doc.EditFamily(family);
            var manager = familyDoc.FamilyManager;
            var targets = manager.GetParameters()
                .Where(p => p.IsShared && map.ContainsKey(p.GUID))
                .ToList();
            if (targets.Count == 0)
                return new FamilyRenameResult(family.Name, 0, new List<string> { "No mapped parameters in this family." }, false);

            using (var transaction = new Transaction(familyDoc, "Swap shared parameters"))
            {
                transaction.Start();
                var tempIndex = 0;
                foreach (var parameter in targets)
                {
                    var oldName = parameter.Definition.Name;
                    if (!definitions.TryGetValue(map[parameter.GUID], out var newDefinition))
                    {
                        messages.Add($"{oldName}: target not found in the shared parameter file — skipped.");
                        continue;
                    }
                    if (manager.get_Parameter(newDefinition.GUID) is not null || manager.get_Parameter(newDefinition.Name) is not null)
                    {
                        messages.Add($"{oldName}: family already has {newDefinition.Name} — skipped.");
                        continue;
                    }

                    var group = parameter.Definition is InternalDefinition internalDefinition ? internalDefinition.GetGroupTypeId() : new ForgeTypeId();
                    var isInstance = parameter.IsInstance;
                    string tempName;
                    do { tempName = $"OttawaSwapTemp_{tempIndex++}"; } while (manager.get_Parameter(tempName) is not null);

                    using var step = new SubTransaction(familyDoc);
                    step.Start();
                    try
                    {
                        var temporary = manager.ReplaceParameter(parameter, tempName, group, isInstance);
                        manager.ReplaceParameter(temporary, newDefinition, group, isInstance);
                        step.Commit();
                        swapped++;
                        messages.Add($"{oldName} → {newDefinition.Name}");
                    }
                    catch (Exception ex)
                    {
                        step.RollBack();
                        messages.Add($"{oldName}: {ex.Message}");
                    }
                }

                if (swapped == 0)
                {
                    transaction.RollBack();
                    return new FamilyRenameResult(family.Name, 0, messages, false);
                }
                transaction.Commit();
            }

            familyDoc.LoadFamily(doc, new OverwriteLoadOptions());
            return new FamilyRenameResult(family.Name, swapped, messages, false);
        }
        catch (Exception ex)
        {
            messages.Add($"Family not updated: {ex.Message}");
            return new FamilyRenameResult(family.Name, 0, messages, true);
        }
        finally
        {
            familyDoc?.Close(false);
        }
    }

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
