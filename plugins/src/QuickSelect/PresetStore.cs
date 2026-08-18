// UseWPF+UseWindowsForms together drops System.IO from implicit global
// usings (see Shared/LicenseStore.cs) — needed here for Path/File/Directory.
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

using System.Text.Json;
using BIMFlow.Shared;

namespace BIMFlow.QuickSelect;

public record FilterPreset(string Name, string CategoryName, List<FilterRule> Rules);

public static class PresetStore
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIMFlow");
    private static readonly string PresetsFile = Path.Combine(ConfigDir, "quickselect-presets.json");

    public static List<FilterPreset> Load()
    {
        if (!File.Exists(PresetsFile)) return new List<FilterPreset>();
        try
        {
            var json = File.ReadAllText(PresetsFile);
            return JsonSerializer.Deserialize<List<FilterPreset>>(json) ?? new List<FilterPreset>();
        }
        catch (JsonException)
        {
            return new List<FilterPreset>();
        }
    }

    public static void Save(List<FilterPreset> presets)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(PresetsFile, JsonSerializer.Serialize(presets));
    }
}
