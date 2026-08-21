namespace OttawaWork.Shared;

/// <summary>
/// Revit rejects duplicate Mark/Number/Name values within their uniqueness
/// scope, so renumbering a set of elements can fail mid-way when two of them
/// are swapping values. Applying every new value through a temporary,
/// guaranteed-unique prefix first — then the real values — means old and new
/// values never collide with each other.
/// </summary>
public static class TwoPassRenamer
{
    public static void Apply(IReadOnlyList<(Action<string> setValue, string newValue)> renames)
    {
        var tempPrefix = $"__ottawatools_tmp_{Guid.NewGuid():N}_";

        for (var i = 0; i < renames.Count; i++)
            renames[i].setValue(tempPrefix + i);

        foreach (var (setValue, newValue) in renames)
            setValue(newValue);
    }
}
