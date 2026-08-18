// Explicit aliases, not a plain "using System.IO;" — Shared now has both
// UseWindowsForms and UseWPF, and that combination drops System.IO from
// the SDK's implicit global usings (rather than adding it alongside the
// WPF ones), so bare Path/File/Directory stopped resolving. A plain
// System.IO using would then risk colliding with System.Windows.Shapes.Path
// (also implicitly in scope for a WPF project) — same class of ambiguity
// as the WinForms/WPF/Revit type-name collisions elsewhere in this SDK.
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

using System.Text.Json;

namespace BIMFlow.Shared;

public record CachedLicense(bool Valid, DateTime CheckedAtUtc);

/// <summary>
/// Local, per-workstation storage for the activated license key and the last
/// online validation result, so plugins keep working briefly if BIMFlow is
/// unreachable (offline grace period) instead of failing closed immediately.
/// </summary>
public static class LicenseStore
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIMFlow");

    private static readonly string KeyFile = Path.Combine(ConfigDir, "license.key");
    private static readonly string CacheFile = Path.Combine(ConfigDir, "license-cache.json");

    public static string? ReadKey() =>
        File.Exists(KeyFile) ? File.ReadAllText(KeyFile).Trim() : null;

    public static void SaveKey(string key)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(KeyFile, key.Trim());
    }

    public static CachedLicense? ReadCache(string pluginSlug, string key)
    {
        var all = ReadAllCache();
        return all.TryGetValue(CacheKey(pluginSlug, key), out var entry) ? entry : null;
    }

    public static void WriteCache(string pluginSlug, string key, bool valid)
    {
        Directory.CreateDirectory(ConfigDir);
        var all = ReadAllCache();
        all[CacheKey(pluginSlug, key)] = new CachedLicense(valid, DateTime.UtcNow);
        File.WriteAllText(CacheFile, JsonSerializer.Serialize(all));
    }

    private static string CacheKey(string pluginSlug, string key) => $"{pluginSlug}:{key}";

    private static Dictionary<string, CachedLicense> ReadAllCache()
    {
        if (!File.Exists(CacheFile)) return new Dictionary<string, CachedLicense>();
        try
        {
            var text = File.ReadAllText(CacheFile);
            return JsonSerializer.Deserialize<Dictionary<string, CachedLicense>>(text)
                   ?? new Dictionary<string, CachedLicense>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, CachedLicense>();
        }
    }
}
