#!/usr/bin/env python3
"""Flips the 25 newly-implemented plugins to status:live with copy that
accurately reflects the (often intentionally scoped-down) real implementation."""
import json
import os

CATALOG_PATH = os.path.join(os.path.dirname(__file__), "..", "..", "data", "plugins.json")

UPDATES = {
    "sheetforge": {
        "description": "Reads a sheet list from CSV (SheetNumber, SheetName, and optionally TitleBlockFamily/TitleBlockType/ViewName) and creates every sheet in one operation, placing a view on each sheet when one is specified.",
        "features": [
            "CSV-driven batch sheet creation",
            "Per-row title block selection, falling back to the first loaded type",
            "Optional view placement per sheet",
            "Skips and reports rows that fail (duplicate numbers, etc.)",
        ],
    },
    "viewsync": {
        "description": "Pick a view template and a batch of views, and ViewSync applies it to every one that matches the template's view type in a single transaction.",
        "features": [
            "Bulk view template assignment",
            "Automatically skips views with an incompatible view type",
            "Batch target selection with checklist UI",
            "Undo-safe single transaction",
        ],
    },
    "revisiontrack": {
        "description": "Bulk-adds a new or existing revision to a chosen set of sheets' revision tables, and sets who it was issued to/by across all of them at once. Scoped to the revision schedule — drawing clouds needs explicit sketch geometry per change, so this doesn't attempt to generate them automatically.",
        "features": [
            "Bulk-add a revision to many sheets in one pass",
            "Create a new revision or reuse an existing one",
            "Set Issued To / Issued By across the batch",
            "Uses Revit's own additional-revisions-on-sheet mechanism",
        ],
    },
    "titleblockupdater": {
        "description": "Bulk-sets one text parameter (project info, address, consultant name, etc.) across every title block instance in the current project.",
        "features": [
            "Bulk parameter update across all title block instances",
            "Auto-detects editable text parameters on your title block",
            "Single-transaction, fully undo-able",
            "Scoped to the open document — multi-file batch mode is on the roadmap",
        ],
    },
    "dimensionauto": {
        "description": "Auto-dimensions grid-to-grid spacing in the active view — the one dimensioning target Revit can resolve unambiguously without wall-layer geometry analysis.",
        "features": [
            "Automatic grid-to-grid dimension strings",
            "Groups grids by direction (vertical/horizontal) automatically",
            "Places a dimension line offset from the grid extents",
            "Wall-face and opening dimensioning are on the roadmap",
        ],
    },
    "tagallplus": {
        "description": "Tags every untagged element of the categories you choose in the active view, with a small deterministic offset per tag to reduce exact overlaps.",
        "features": [
            "Batch-tag untagged elements by category",
            "Skips elements that already have a tag in the view",
            "Uses each category's default tag type",
            "Reports how many were tagged vs. skipped",
        ],
    },
    "warningresolver": {
        "description": "Groups every warning in the model by message and severity so you can triage what matters, with jump-to-element for each group. Scoped to reporting — most warning types don't have a single safe automated fix, so this doesn't attempt one.",
        "features": [
            "Groups warnings by message text and severity",
            "Element counts per warning type",
            "Select-in-model for any warning group",
            "Model-wide scan via Revit's own warnings API",
        ],
    },
    "standardschecker": {
        "description": "Checks view names or sheet numbers/names against one approved regex pattern and flags anything that doesn't match, with jump-to-element.",
        "features": [
            "Regex-based naming audit for views, sheet numbers, or sheet names",
            "Results list with select-in-model",
            "Runs in seconds even on large projects",
            "Multi-rule shareable profiles are on the roadmap",
        ],
    },
    "modelhealthdashboard": {
        "description": "Logs element, warning, view, and sheet counts to a local per-model history file every time you run it, and shows the trend since your last run.",
        "features": [
            "Element/warning/view/sheet count snapshot on demand",
            "Local history log per model",
            "Trend comparison against the previous run",
            "Automatic on-save logging is on the roadmap",
        ],
    },
    "roomfinisher": {
        "description": "Scans every room for two issues: unplaced (no location) and unenclosed (placed but zero area, meaning the boundary isn't closed), with a jump-to-room results list.",
        "features": [
            "Model-wide unplaced/unenclosed room scan",
            "Jump-to-room results list",
            "Runs against every level in one pass",
            "Redundant-room detection is on the roadmap",
        ],
    },
    "parambatcheditor": {
        "description": "Bulk-sets one parameter to one value across every element of a category that matches up to two filter rules. Covers the single most common bulk-edit case in one pass — a full spreadsheet-style grid with per-row editing is a larger UI project for later.",
        "features": [
            "Category + up to 2 filter rules to scope the edit",
            "Bulk-set one parameter across every match",
            "Single-transaction, fully undo-able",
            "Spreadsheet-style multi-parameter grid editing is on the roadmap",
        ],
    },
    "familytypemanager": {
        "description": "Batch-renames family/system types within a category (find/replace, prefix/suffix, regex), then offers to purge any types left with zero placed instances.",
        "features": [
            "Batch rename types with find/replace, prefix/suffix, or regex",
            "Unused-type detection and one-click purge",
            "Category-scoped so you're not hunting through the whole project browser",
            "Live preview before committing renames",
        ],
    },
    "familyauditor": {
        "description": "Opens every loadable family already in the project in the background, reports nesting depth and parameter/type counts for each, then closes without saving — flags families that look heavily nested or parameter-bloated.",
        "features": [
            "Scans every loaded family via Revit's own family editor",
            "Reports nested-family count, type count, and parameter count",
            "Flags heavily-nested or parameter-heavy families",
            "Confirms before running, since it opens every family in the background",
        ],
    },
    "parametermapper": {
        "description": "Copies one parameter's value from a selected source element to every element of a chosen category that matches an optional filter. Covers the simpler, still very common 'copy this value onto that set' case — relationship-aware mapping (room to hosted door) needs reliable host/room lookups this pass doesn't attempt.",
        "features": [
            "Pick a source element and parameter to copy",
            "Target by category with up to 2 filter rules",
            "Supports text, number, and integer parameters",
            "Single-transaction, fully undo-able",
        ],
    },
    "connectoralign": {
        "description": "Scans duct, pipe, cable tray, and conduit connectors for ones that are unconnected but sit within a small tolerance of another unconnected connector — the classic near-miss that should be joined but isn't. Reports candidates for review rather than moving geometry automatically.",
        "features": [
            "System-wide unconnected-connector scan",
            "Near-miss detection within a configurable tolerance",
            "Jump-to-elements for each candidate pair",
            "Detection only — no automatic geometry changes",
        ],
    },
    "systemcolorcoder": {
        "description": "Colors every duct/pipe/cable tray/conduit element in the active view by its System Name, using direct per-element graphic overrides.",
        "features": [
            "Auto color-codes by System Name in the active view",
            "Distinct color per system, applied in one transaction",
            "Skips elements with no system assigned",
            "Saved view-filter version (visible in the V/G dialog) is on the roadmap",
        ],
    },
    "equipmentscheduler": {
        "description": "Generates a Mechanical Equipment schedule with a sensible default set of fields in one click. External cut-sheet data linking is a separate, larger feature — this covers the 'give me a starting schedule' need on its own.",
        "features": [
            "One-click Mechanical Equipment schedule generation",
            "Sensible default field set (type, mark, manufacturer, model, comments)",
            "Auto-names to avoid colliding with existing schedules",
            "External cut-sheet import is on the roadmap",
        ],
    },
    "insulationmanager": {
        "description": "Applies a chosen insulation type and thickness to every selected pipe or duct that doesn't already have insulation.",
        "features": [
            "Batch-apply insulation type + thickness to a selection",
            "Skips elements that already have insulation",
            "Works across mixed pipe/duct selections",
            "Location-aware rules (interior/exterior/plenum) are on the roadmap",
        ],
    },
    "mepclashprecheck": {
        "description": "Flags MEP-vs-structural element pairs whose bounding boxes overlap in the active view, as a fast in-Revit pre-check before a full coordination review. Bounding-box heuristic, not true solid-geometry clash detection.",
        "features": [
            "Bounding-box overlap check between MEP and structural categories",
            "Runs entirely inside Revit — no export round-trip",
            "Jump-to-elements for each candidate pair",
            "Full solid-geometry clash detection is on the roadmap",
        ],
    },
    "framingchecker": {
        "description": "Flags structural framing members whose start or end point isn't near any other structural element — a strong hint of a missing connection, found via bounding-box proximity rather than the analytical model.",
        "features": [
            "Model-wide floating-member scan",
            "Flags which end (start/end/both) is disconnected",
            "Jump-to-member results list",
            "True connection-status via the analytical model is on the roadmap",
        ],
    },
    "gridgenerator": {
        "description": "Generates straight structural grids from a CSV (Name, Direction, Position, Start, End). A CSV of positions is something every office can produce reliably from their own standard — parsing positions out of an imported CAD file varies too much between office conventions to automate confidently.",
        "features": [
            "CSV-driven grid generation",
            "Supports vertical and horizontal grids",
            "Auto-names each grid from the CSV",
            "Reports rows that fail (duplicate names, bad data)",
        ],
    },
    "excel2revit": {
        "description": "Export mode dumps a chosen schedule to CSV via Revit's own schedule export. Import mode drives arbitrary parameter values on any elements from a CSV (ElementId + one column per parameter name).",
        "features": [
            "Schedule export to CSV via Revit's native export",
            "Generic ElementId-matched parameter import — any category, any parameters",
            "Per-field type checking (skips values that don't fit)",
            "Change summary after import",
        ],
    },
    "ifcexportqa": {
        "description": "Shows a pre-flight summary of what's about to be exported by category, then runs Revit's own IFC export with your chosen version. Deep per-element property-set validation needs the same schema depth a full IFC QA tool would — this covers the 'know what you're exporting, then export cleanly' half.",
        "features": [
            "Pre-export category/element-count summary",
            "IFC2x3 and IFC4 export via Revit's native exporter",
            "One dialog from ribbon click to exported file",
            "Deep property-set validation is on the roadmap",
        ],
    },
    "linkedmodelauditor": {
        "description": "Lists every linked model instance with its load status, pin state, and workset in one table.",
        "features": [
            "Link status (loaded/unloaded/not found) via Revit's own link API",
            "Pin state and workset for every link instance",
            "Select-in-model for any link",
            "Last-saved staleness comparison is on the roadmap",
        ],
    },
    "worksetmonitor": {
        "description": "Shows element counts per user workset, plus open/closed state and default visibility — the model-size half of workset health that's reliably available via the public API.",
        "features": [
            "Element count per user workset",
            "Open/closed and default-visibility state",
            "Total element count across all worksets",
            "Per-user ownership and last-sync time aren't exposed as workset-level APIs — out of scope for this pass",
        ],
    },
}

with open(CATALOG_PATH) as f:
    catalog = json.load(f)

updated = 0
for plugin in catalog["plugins"]:
    if plugin["slug"] in UPDATES:
        plugin["status"] = "live"
        plugin["description"] = UPDATES[plugin["slug"]]["description"]
        plugin["features"] = UPDATES[plugin["slug"]]["features"]
        updated += 1

with open(CATALOG_PATH, "w") as f:
    json.dump(catalog, f, indent=2)
    f.write("\n")

live_count = sum(1 for p in catalog["plugins"] if p["status"] == "live")
print(f"Updated {updated} plugins to live. Catalog now has {live_count} live / {len(catalog['plugins'])} total.")
