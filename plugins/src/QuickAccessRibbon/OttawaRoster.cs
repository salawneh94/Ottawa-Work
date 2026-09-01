namespace OttawaWork.QuickAccessRibbon;

/// <summary>One row per ribbon entry — folder doubles as DLL name (OttawaWork.<Folder>.dll),
/// command class (OttawaWork.<Folder>.Command) and icon key (Icons/3d/<Folder>_16.png), the same
/// convention every plugin's own .csproj/Application.cs already follows. Moved here from the
/// BIMFlow-catalog's PluginRoster.cs, which this build doesn't have — see OttawaRoster below.
/// PulldownGroup is optional: entries sharing the same group name are nested as sub-buttons
/// under one named pulldown/flyout button instead of sitting flat in the panel. Not currently
/// used by any entry below — general-purpose ribbon infrastructure in case a future panel needs
/// a nested-flyout layout instead of the flat stacked-column layout every panel uses today.</summary>
public record RosterEntry(string Folder, string Panel, string Text, string Tooltip, bool Hero, string? PulldownGroup = null);

/// <summary>
/// The internal Ottawa-Work firm suite's ribbon roster — a curated subset of
/// the sibling BIMFlow repo's full 75+ plugin catalog (that repo's
/// PluginRoster.cs, which this build doesn't have at all), grouped into the
/// firm's own 8 named panels rather than the catalog's marketing panel
/// names (Select and Highlight split apart from the original combined
/// "Highlight & View" panel, matching the reference tool's own Select/
/// Highlight layout). This is what OttawaWork.QuickAccessRibbon's Application.cs
/// builds the "Ottawa Tools" ribbon tab from in this build. Within Select and
/// Highlight specifically, entry order is load-bearing — see the comment
/// above each panel's entries below.
/// </summary>
public static class OttawaRoster
{
    public static readonly RosterEntry[] Entries =
    {
        // Panel 1: Select — direct-selection quick-select tools, matching
        // the reference (Nexion-style) Direct Selection layout: 4 stacked
        // columns of 3 small buttons each. Every button instantly sets the
        // active Revit selection (uidoc.Selection.SetElementIds) to every
        // matching element visible in the active view — no dialog, no
        // transaction — see Shared/QuickSelectEngine.cs. Entry order here is
        // load-bearing: QuickAccessRibbon/Application.cs's AddStacked packs
        // this flat list into groups of 3 in array order, so each run of 3
        // below becomes one vertical column, in this exact sequence.
        new("SelectAllWalls", "Select", "All Walls", "Select every wall visible in the active view.", false),
        new("SelectExtWalls", "Select", "Ext Walls", "Select every exterior wall visible in the active view.", false),
        new("SelectIntWalls", "Select", "Int Walls", "Select every interior wall visible in the active view.", false),
        new("SelectAllDoors", "Select", "All Doors", "Select every door visible in the active view.", false),
        new("SelectExtDoors", "Select", "Ext Doors", "Select every exterior door visible in the active view.", false),
        new("SelectIntDoors", "Select", "Int Doors", "Select every interior door visible in the active view.", false),
        new("SelectFloors", "Select", "Floors", "Select every floor visible in the active view.", false),
        new("SelectWindows", "Select", "Windows", "Select every window visible in the active view.", false),
        new("SelectRoofs", "Select", "Roofs", "Select every roof visible in the active view.", false),
        new("SelectCeilings", "Select", "Ceilings", "Select every ceiling visible in the active view.", false),
        new("SelectBeams", "Select", "Beams", "Select every structural beam visible in the active view.", false),
        new("SelectRooms", "Select", "Rooms", "Select every room visible in the active view.", false),

        // Panel 2: Highlight — view-graphics tools (color overrides,
        // point-cloud comparison), separated from Select above to match the
        // reference tool's own Select/Highlight split. The old "Highlight"
        // card-grid dashboard entry (a launcher-of-launchers for these same
        // 5 tools) was removed entirely: it posted each card's command via
        // UIApplication.PostCommand(RevitCommandId.LookupCommandId(fullClassName)),
        // but LookupCommandId's string argument has to be Revit's own internal
        // ribbon-item identifier (derived from tab/panel/button internal
        // names), not a bare C# class name — confirmed dead on a real click
        // in Revit (nothing happened), which this repo's compile-only CI
        // could never have caught. Deleting it cost nothing: every tool it
        // would have launched is already its own standalone button below.
        new("HighlightExterior", "Highlight", "HL Exterior", "Toggle a red color highlight on every exterior wall in the active view.", false),
        new("HighlightInterior", "Highlight", "HL Interior", "Toggle a blue color highlight on every interior wall in the active view.", false),
        new("OverrideByParam", "Highlight", "Color Code", "Color-code any category by any parameter value — pick a palette, preview live, then apply as real persistent view filters.", false),
        new("PointCloudColorizer", "Highlight", "PC Color", "Tint point cloud instances with a preset or custom color, all in view or just the selection.", false),
        new("PointCloudHeatmap", "Highlight", "PC Heatmap", "Compare walls against point cloud scan data and color-code deviations: green/yellow/red by tolerance.", false),

        // Panel 3: Data & Excel
        new("Excel2Revit", "Data & Excel", "Single Excel Sync", "Browse and batch-export any schedule to Excel/CSV/tab-delimited, or import an edited export back as parameter values.", false),
        new("BatchExcelSync", "Data & Excel", "Batch Excel Sync", "Export any category's parameters to Excel, edit them, then re-import with a live diff and approve exactly which changes to commit.", true),
        new("IFCExportQA", "Data & Excel", "IFC Mapping & Exporter", "Pre-flight checks, IFC class mapping presets, and clean IFC export.", false),

        // Panel 4: Renumbering
        // UniqueNumbering supersedes the old Room Renumber (Rooms only, one
        // hardcoded field, no preview) — any of the ~19 common model
        // categories, any String-storage parameter, one or more rules at
        // once, and a preview before anything is written. Grid Renumber is
        // untouched: renaming grids/levels is a find/replace transform of
        // existing text, a different operation from sequential numbering by
        // sort order, so folding it into the same rule engine would force
        // two different operations into one UI instead of actually helping.
        new("UniqueNumbering", "Renumbering", "Unique Numbering", "Add numbering rules per parameter for any category, preview every element's existing vs. new value, then assign or clear at once.", false),
        new("GridRenumber", "Renumbering", "Grid Renumber", "Batch rename grids or levels without breaking references.", false),

        // Panel 5: Spatial & Rooms
        new("PlansPerRoom", "Spatial & Rooms", "Plans Per Room", "Build a full room-data sheet set — floor plan, key plan, elevations, wall sections, RCP — with editable naming templates and per-room finish parameters.", false),
        new("RoomTagger", "Spatial & Rooms", "Room Tagger", "Write each room's number/name onto every element found inside it.", false),

        // Panel 6: Annotation & Legends
        new("OverriddenDimensionDetector", "Annotation & Legends", "Overridden Dimension Detector", "Scan every dimension for a manual override or annotation, classify by severity (Falsified/Frozen/Annotated), and fix or select what's flagged.", false),
        new("DimensionEditor", "Annotation & Legends", "Dimension Editor", "Edit a dimension's override value, prefix, and suffix directly.", false),
        new("LegendBuilder", "Annotation & Legends", "Legend Builder", "Auto-build a legend view from every detail component and annotation symbol actually used in the model.", false),
        new("LegendPlacer", "Annotation & Legends", "Legend Placer", "Place a legend on multiple sheets at a consistent position.", false),

        // Panel 7: Model QA/QC
        new("PurgePro", "Model QA/QC", "Purge Pro", "Repeat Revit's purge-unused until the model stops shrinking.", false),
        new("NamingConventionAudit", "Model QA/QC", "Naming Audit", "Check view, sheet, or family type names against a regex pattern you define.", false),
        new("StairCalculator", "Model QA/QC", "Stair Calculator", "Check stair riser/tread proportions against the 2R+G comfort rule.", false),
        new("UnplacedCleaner", "Model QA/QC", "Unplaced Cleaner", "Find unplaced rooms (never placed on a plan) and delete them in one pass.", false),
        new("ScopeBoxSync", "Model QA/QC", "Scope Box Sync", "Apply a scope box to a batch of selected views.", false),
        new("TitleBlockUpdater", "Model QA/QC", "Title Block Updater", "Batch-update title block info across sheets and linked projects.", false),
        new("RevisionTrack", "Model QA/QC", "Revision Track", "Automate revision clouds and revision schedules across sheet sets.", false),

        // Panel 8: Parameters
        // ParamBatchEditor's old spreadsheet-grid bulk edit is superseded by
        // ParamPowerSuite's own Bulk Set tab — consolidated per request, not
        // duplicated; the ParamBatchEditor plugin files themselves are left
        // on disk untouched, just no longer wired into this ribbon.
        new("ParamPowerSuite", "Parameters", "Param Power Suite", "Bulk-edit parameters across every loaded element: set, find/replace, transform, copy, combine, jam to shared, or create a new bound parameter — all in one tabbed workbench.", true),
        new("Din276CostEstimator", "Parameters", "DIN 276 Costs", "Classify elements into DIN 276 Kostengruppen and price them against your own unit rates — live quantities, editable rates, exportable report.", true),
    };
}
