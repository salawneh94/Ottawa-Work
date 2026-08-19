namespace BIMFlow.QuickAccessRibbon;

/// <summary>One row per catalog plugin — folder doubles as DLL name (BIMFlow.<Folder>.dll),
/// command class (BIMFlow.<Folder>.Command) and icon key (Icons/<Folder>_16.png), all by the
/// same established convention every plugin's own .csproj/Application.cs already follows.
/// Generated from plugins/src/*/Application.cs + data/plugins.json + data/plugin-impact.json —
/// see the git history for the generator script if this needs regenerating after adding a plugin.</summary>
public record RosterEntry(string Folder, string Panel, string Text, string Tooltip, bool Hero);

public static class PluginRoster
{
    // TEMPORARY — this file normally lists all 75 catalog plugins. Trimmed
    // to just 3 for a quick preview build on the temp/three-plugin-preview
    // branch, per request ("temporary... don't do massive changes"). The
    // full list lives untouched on the main branch — restore from there
    // (or `git log` on this file) rather than trying to reconstruct it by
    // hand if this branch needs to grow back.
    public static readonly RosterEntry[] Entries =
    {
        new("BatchScheduleExporter", "Data & Coordination", "BatchScheduleExporter", "Export every schedule in the project to individual CSV files in one pass.", true),
        new("Excel2Revit", "Data & Coordination", "Excel2Revit", "Two-way sync between Excel/CSV and Revit parameters or schedules.", true),
        new("RoomTagger", "Rooms", "RoomTagger", "Write each room's number/name onto every element found inside it.", true),
    };
}
