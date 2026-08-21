using System.Text.RegularExpressions;

namespace OttawaWork.Shared;

/// <summary>Shared find/replace + prefix/suffix renaming rule used by every batch-rename tool.</summary>
public static class RenameRules
{
    public static string Apply(string original, string find, string replace, string prefix, string suffix, bool useRegex)
    {
        var result = original;

        if (!string.IsNullOrEmpty(find))
        {
            result = useRegex
                ? Regex.Replace(result, find, replace ?? string.Empty)
                : result.Replace(find, replace ?? string.Empty);
        }

        return $"{prefix}{result}{suffix}";
    }
}
