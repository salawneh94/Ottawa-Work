namespace BIMFlow.QuickAccessRibbon;

/// <summary>One row per ribbon entry — folder doubles as DLL name (BIMFlow.<Folder>.dll),
/// command class (BIMFlow.<Folder>.Command) and icon key (Icons/<Folder>_16.png), the same
/// convention every plugin's own .csproj/Application.cs already follows. Moved here from the
/// BIMFlow-catalog's PluginRoster.cs, which this build doesn't have — see OttawaRoster below.
/// PulldownGroup is optional: entries sharing the same group name are nested as sub-buttons
/// under one named pulldown/flyout button instead of sitting flat in the panel — matching how
/// the reference tool's "Highlight" button opens a menu of HL Exterior/HL Interior rather than
/// showing them as their own top-level buttons.</summary>
public record RosterEntry(string Folder, string Panel, string Text, string Tooltip, bool Hero, string? PulldownGroup = null);

/// <summary>
/// The internal Ottawa-Work firm suite's ribbon roster — a curated subset of
/// the sibling BIMFlow repo's full 75+ plugin catalog (that repo's
/// PluginRoster.cs, which this build doesn't have at all), grouped into the
/// firm's own 7 named panels rather than the catalog's marketing panel
/// names. This is what BIMFlow.QuickAccessRibbon's Application.cs builds
/// the "Ottawa Tools" ribbon tab from in this build.
/// </summary>
public static class OttawaRoster
{
    public static readonly RosterEntry[] Entries =
    {
        // Panel 1: Highlight & View
        new("OverrideByParam", "Highlight & View", "Color Code", "Color-code any category by any parameter value — pick a palette, preview live, then apply as real persistent view filters.", false),
        new("HighlightExterior", "Highlight & View", "HL Exterior", "Toggle a red color highlight on every exterior wall in the active view.", false, PulldownGroup: "Highlight"),
        new("HighlightInterior", "Highlight & View", "HL Interior", "Toggle a blue color highlight on every interior wall in the active view.", false, PulldownGroup: "Highlight"),
        new("PointCloudColorizer", "Highlight & View", "PC Color", "Tint point cloud instances with a preset or custom color, all in view or just the selection.", false),
        new("PointCloudHeatmap", "Highlight & View", "PC Heatmap", "Compare walls against point cloud scan data and color-code deviations: green/yellow/red by tolerance.", false),

        // Panel 2: Data & Excel
        new("Excel2Revit", "Data & Excel", "Single Excel Sync", "Two-way sync between Excel/CSV and Revit parameters or schedules.", false),
        new("BatchExcelSync", "Data & Excel", "Batch Excel Sync", "Export any category's parameters to Excel, edit them, then re-import with a live diff and approve exactly which changes to commit.", false),
        new("IFCExportQA", "Data & Excel", "IFC Mapping & Exporter", "Pre-flight checks, IFC class mapping presets, and clean IFC export.", false),

        // Panel 3: Renumbering
        new("RoomRenumber", "Renumbering", "Room Renumber", "Renumber rooms by direction of travel or zone.", false),
        new("GridRenumber", "Renumbering", "Grid Renumber", "Batch rename grids or levels without breaking references.", false),

        // Panel 4: Spatial & Rooms
        new("PlansPerRoom", "Spatial & Rooms", "Plans Per Room", "Build a full room-data sheet set — floor plan, key plan, elevations, wall sections, RCP — with editable naming templates and per-room finish parameters.", false),
        new("RoomTagger", "Spatial & Rooms", "Room Tagger", "Write each room's number/name onto every element found inside it.", false),

        // Panel 5: Annotation & Legends
        new("DimensionEditor", "Annotation & Legends", "Dimension Editor", "Edit a dimension's override value, prefix, and suffix directly.", false),
        new("LegendBuilder", "Annotation & Legends", "Legend Builder", "Auto-build a legend view from every detail component and annotation symbol actually used in the model.", false),
        new("LegendPlacer", "Annotation & Legends", "Legend Placer", "Place a legend on multiple sheets at a consistent position.", false),

        // Panel 6: Model QA/QC
        new("PurgePro", "Model QA/QC", "Purge Pro", "Repeat Revit's purge-unused until the model stops shrinking.", false),
        new("NamingConventionAudit", "Model QA/QC", "Naming Audit", "Check view, sheet, or family type names against a regex pattern you define.", false),
        new("StairCalculator", "Model QA/QC", "Stair Calculator", "Check stair riser/tread proportions against the 2R+G comfort rule.", false),
        new("UnplacedCleaner", "Model QA/QC", "Unplaced Cleaner", "Find unplaced rooms (never placed on a plan) and delete them in one pass.", false),
        new("ScopeBoxSync", "Model QA/QC", "Scope Box Sync", "Apply a scope box to a batch of selected views.", false),
        new("TitleBlockUpdater", "Model QA/QC", "Title Block Updater", "Batch-update title block info across sheets and linked projects.", false),
        new("RevisionTrack", "Model QA/QC", "Revision Track", "Automate revision clouds and revision schedules across sheet sets.", false),

        // Panel 7: Parameters
        new("ParamBatchEditor", "Parameters", "Param Batch Editor", "Bulk-edit parameter values in a spreadsheet-style grid.", false),
    };
}
