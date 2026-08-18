// UseWPF+UseWindowsForms together drops System.IO from implicit global
// usings (see Shared/LicenseStore.cs) — needed here for Path/File/Directory.
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

using System.Text.Json;

namespace BIMFlow.ModelHealthDashboard;

public record HealthEntry(DateTime TimestampUtc, int ElementCount, int WarningCount, int ViewCount, int SheetCount);

public static class HealthLogStore
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIMFlow", "model-health");

    public static List<HealthEntry> Load(string docTitle)
    {
        var path = LogPath(docTitle);
        if (!File.Exists(path)) return new List<HealthEntry>();
        try
        {
            return JsonSerializer.Deserialize<List<HealthEntry>>(File.ReadAllText(path)) ?? new List<HealthEntry>();
        }
        catch (JsonException)
        {
            return new List<HealthEntry>();
        }
    }

    public static void Append(string docTitle, HealthEntry entry)
    {
        Directory.CreateDirectory(LogDir);
        var entries = Load(docTitle);
        entries.Add(entry);
        File.WriteAllText(LogPath(docTitle), JsonSerializer.Serialize(entries));
    }

    private static string LogPath(string docTitle)
    {
        var safeName = string.Concat(docTitle.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(LogDir, $"{safeName}.json");
    }
}
