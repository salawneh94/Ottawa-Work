namespace BIMFlow.SharedParamSync;

public record DiffResult(
    List<SharedParamRecord> OnlyInA,
    List<SharedParamRecord> OnlyInB,
    List<(SharedParamRecord A, SharedParamRecord B)> NameConflicts,
    List<(SharedParamRecord A, SharedParamRecord B)> GuidConflicts);

public static class SharedParamDiff
{
    public static DiffResult Compare(SharedParameterFile a, SharedParameterFile b)
    {
        var guidsInA = a.Params.ToDictionary(p => p.Guid, p => p);
        var guidsInB = b.Params.ToDictionary(p => p.Guid, p => p);
        var namesInA = a.Params.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
        var namesInB = b.Params.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var onlyInA = a.Params.Where(p => !guidsInB.ContainsKey(p.Guid)).ToList();
        var onlyInB = b.Params.Where(p => !guidsInA.ContainsKey(p.Guid)).ToList();

        var guidConflicts = a.Params
            .Where(p => guidsInB.TryGetValue(p.Guid, out var match) && !string.Equals(match.Name, p.Name, StringComparison.OrdinalIgnoreCase))
            .Select(p => (p, guidsInB[p.Guid]))
            .ToList();

        var nameConflicts = a.Params
            .Where(p => namesInB.TryGetValue(p.Name, out var match) && match.Guid != p.Guid)
            .Select(p => (p, namesInB[p.Name]))
            .ToList();

        return new DiffResult(onlyInA, onlyInB, nameConflicts, guidConflicts);
    }

    /// <summary>
    /// Merges B's non-conflicting parameters into A. Parameters whose GUID
    /// already exists in A are skipped (A wins) since Revit tracks shared
    /// parameters by GUID, not name.
    /// </summary>
    public static (List<SharedParamGroup> Groups, List<SharedParamRecord> Params) Merge(
        SharedParameterFile a, SharedParameterFile b)
    {
        var groups = new List<SharedParamGroup>(a.Groups);
        var nextGroupId = groups.Count == 0 ? 1 : groups.Max(g => g.Id) + 1;
        var groupIdByName = groups.ToDictionary(g => g.Name, g => g.Id, StringComparer.OrdinalIgnoreCase);

        var mergedParams = new List<SharedParamRecord>(a.Params);
        var existingGuids = a.Params.Select(p => p.Guid).ToHashSet();

        foreach (var param in b.Params)
        {
            if (existingGuids.Contains(param.Guid)) continue; // A wins on GUID conflicts

            var bGroup = b.Groups.FirstOrDefault(g => g.Id == param.GroupId);
            var groupName = bGroup?.Name ?? "General";

            if (!groupIdByName.TryGetValue(groupName, out var resolvedGroupId))
            {
                resolvedGroupId = nextGroupId++;
                groupIdByName[groupName] = resolvedGroupId;
                groups.Add(new SharedParamGroup(resolvedGroupId, groupName));
            }

            var remappedFields = (string[])param.RawFields.Clone();
            remappedFields[5] = resolvedGroupId.ToString();
            mergedParams.Add(param with { GroupId = resolvedGroupId, RawFields = remappedFields });
            existingGuids.Add(param.Guid);
        }

        return (groups, mergedParams);
    }
}
