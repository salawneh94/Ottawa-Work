#!/usr/bin/env python3
"""Adds 18 plugins inspired by NexionTools' Sheets & Views / Parameters /
Rooms / Highlight / Select categories, plus two new catalog categories
("rooms", "highlight") to house them."""
import json
import os

CATALOG_PATH = os.path.join(os.path.dirname(__file__), "..", "..", "data", "plugins.json")

NEW_CATEGORIES = [
    {"id": "rooms", "name": "Rooms & Spatial"},
    {"id": "highlight", "name": "Highlight & Visualization"},
]

NEW_PLUGINS = [
    {
        "slug": "legendplacer", "name": "LegendPlacer",
        "tagline": "Place a legend on multiple sheets at a consistent position.",
        "category": "sheets-docs", "priceUSD": 25, "status": "live", "icon": "🗺️",
        "description": "Places a chosen legend view onto any number of sheets at the same coordinate in one pass — legends are the one Revit view type that can live on multiple sheets at once, so this just automates placing (and re-placing) them consistently.",
        "features": [
            "Batch-place one legend onto many sheets",
            "Same position on every sheet, set once",
            "Skips sheets that already have it placed",
            "Single-transaction, fully undo-able",
        ],
    },
    {
        "slug": "autolegendbuilder", "name": "AutoLegendBuilder",
        "tagline": "Audit which line styles, fill patterns, and detail components are actually used — and how often.",
        "category": "sheets-docs", "priceUSD": 35, "status": "live", "icon": "📊",
        "description": "Scans the model for every line pattern, fill pattern, and detail component actually placed, and reports usage counts against everything loaded — so you know what belongs in your legend and what's dead weight.",
        "features": [
            "Usage counts for line patterns, fill patterns, and detail components",
            "Flags loaded-but-unused entries",
            "Exportable inventory report",
            "Model-wide scan in one pass",
        ],
    },
    {
        "slug": "overridebyparam", "name": "OverrideByParam",
        "tagline": "Color-code any category by any parameter value in the active view.",
        "category": "highlight", "priceUSD": 29, "status": "live", "icon": "🎨",
        "description": "Pick any category and any parameter, and OverrideByParam assigns a distinct color to each value and applies it across the active view in one pass — the general-purpose version of color-by-value, not locked to one discipline.",
        "features": [
            "Works with any category and any parameter",
            "Auto-assigns a distinct color per value",
            "Single-transaction apply, fully undo-able",
            "Pairs well with QuickSelect+ for follow-up filtering",
        ],
    },
    {
        "slug": "highlightwalls", "name": "HighlightWalls",
        "tagline": "Toggle an interior/exterior color highlight on every wall in the active view.",
        "category": "highlight", "priceUSD": 19, "status": "live", "icon": "🧱",
        "description": "One click colors every wall in the active view by its Function parameter — interior one color, exterior another — and a second click clears it. A fast visual sanity check before issuing drawings.",
        "features": [
            "Toggle on/off — the same command clears the highlight",
            "Colors by the Wall Function parameter (Interior/Exterior)",
            "Applies across the whole active view",
            "No saved filters left behind when you clear it",
        ],
    },
    {
        "slug": "selectbycategory", "name": "SelectByCategory",
        "tagline": "One-click select every element of a category, in the view or the whole project.",
        "category": "productivity", "priceUSD": 15, "status": "live", "icon": "🎯",
        "description": "Pick a category and a scope (active view, or whole project in a 3D view) and select every instance in one click — the fast, zero-configuration complement to QuickSelect+'s rule builder.",
        "features": [
            "Select all instances of any category",
            "Active-view or whole-project scope",
            "No filter setup needed for the common case",
            "Selects directly into the active Revit selection set",
        ],
    },
    {
        "slug": "viewmatrix", "name": "ViewMatrix",
        "tagline": "Audit scope box and crop region settings across every view.",
        "category": "qaqc-cleanup", "priceUSD": 29, "status": "live", "icon": "📐",
        "description": "Lists every view with its assigned scope box and crop region state side by side, so inconsistencies (a view that should share a scope box but doesn't) jump out instead of hiding in the project browser.",
        "features": [
            "Scope box + crop state for every view in one table",
            "Flags views missing a scope box other views in the same set have",
            "Jump-to-view from the results list",
            "Exportable audit report",
        ],
    },
    {
        "slug": "parameterformulapropagator", "name": "ParameterFormulaPropagator",
        "tagline": "Generate parameter values from a template pattern and sequential numbering.",
        "category": "families-parameters", "priceUSD": 29, "status": "live", "icon": "🔤",
        "description": "Define a template like \"{Level}-EQ-{seq:000}\" and ParameterFormulaPropagator fills a target parameter across a filtered selection, substituting each element's level and an auto-incrementing sequence number.",
        "features": [
            "Template placeholders for level, category, and a sequence counter",
            "Live preview of generated values before commit",
            "Category + filter rule targeting",
            "Single-transaction, fully undo-able",
        ],
    },
    {
        "slug": "scheduleblankdetector", "name": "ScheduleBlankDetector",
        "tagline": "Find blank fields in a schedule and score how complete it is.",
        "category": "qaqc-cleanup", "priceUSD": 25, "status": "live", "icon": "🧮",
        "description": "Scans a chosen schedule's underlying elements for blank values in scheduled fields, and reports a completeness score plus a jump-to list of the rows that need attention.",
        "features": [
            "Per-field blank-cell detection on any schedule",
            "Overall completeness percentage",
            "Jump-to-element for incomplete rows",
            "Works on any schedule already in the project",
        ],
    },
    {
        "slug": "excelasdraft", "name": "ExcelAsDraft",
        "tagline": "Import a CSV as a visual grid table in a drafting view, with one-click refresh.",
        "category": "data-interop", "priceUSD": 35, "status": "live", "icon": "📋",
        "description": "Draws a CSV's rows and columns as an actual grid (lines + text) inside a drafting view — for tables that just need to be readable on a sheet, not tied to a schedulable category. Re-run the same command to refresh from the source file.",
        "features": [
            "CSV-to-drafting-view grid table",
            "Grid lines and text notes drawn to match column widths",
            "Re-run to refresh from the same source file",
            "No category or schedule required — works for any tabular data",
        ],
    },
    {
        "slug": "roominventory", "name": "RoomInventory",
        "tagline": "List every element inside each room, with a category breakdown.",
        "category": "rooms", "priceUSD": 35, "status": "live", "icon": "📦",
        "description": "For every room, finds every element whose location point falls inside its boundary and reports a category breakdown — a spatial inventory for room-by-room QA or handover documentation.",
        "features": [
            "Point-in-room test across chosen categories",
            "Per-room category breakdown",
            "CSV export",
            "Model-wide scan, one pass",
        ],
    },
    {
        "slug": "roomfinishschedule", "name": "RoomFinishSchedule",
        "tagline": "Generate a room finish schedule and export it to CSV.",
        "category": "rooms", "priceUSD": 25, "status": "live", "icon": "🧾",
        "description": "Creates a schedule of every room's floor, wall, ceiling, and base finish parameters in one click, and exports it straight to CSV for a finish area takeoff.",
        "features": [
            "One-click room finish schedule generation",
            "Covers floor, wall, ceiling, and base finish fields",
            "Direct CSV export",
            "Auto-names to avoid colliding with existing schedules",
        ],
    },
    {
        "slug": "modelceilings", "name": "ModelCeilings",
        "tagline": "Auto-create drop ceilings from room boundaries in the active floor plan.",
        "category": "rooms", "priceUSD": 39, "status": "live", "icon": "🏚️",
        "description": "Generates a ceiling for every room in the active floor plan directly from its boundary, at a height you set — turns a room-by-room ceiling-sketching chore into one click.",
        "features": [
            "Batch ceiling creation from room boundaries",
            "Configurable ceiling type and height offset",
            "Skips rooms whose boundary can't be resolved into a closed loop",
            "Single-transaction, fully undo-able",
        ],
    },
    {
        "slug": "uniquenumbering", "name": "UniqueNumbering",
        "tagline": "Multi-parameter sequential numbering with a rule builder and live preview.",
        "category": "families-parameters", "priceUSD": 39, "status": "in-development", "icon": "🔢",
        "description": "A rule builder for numbering schemes that span multiple parameters at once (e.g. level + zone + sequence), with a live preview before committing — a bigger UI project than a single-parameter renumber tool, so it's being built as its own thing rather than bolted onto the existing renumbering tools.",
        "features": [
            "Multi-parameter numbering rule builder",
            "Live preview before commit",
            "Reusable numbering scheme presets",
            "Collision-safe two-pass apply",
        ],
    },
    {
        "slug": "plansperroom", "name": "PlansPerRoom",
        "tagline": "Export a comprehensive room data book with a per-room floor plan for every room.",
        "category": "rooms", "priceUSD": 45, "status": "in-development", "icon": "📘",
        "description": "Generates one cropped floor plan view per room and compiles them into a room data book — needs reliable per-room view duplication and crop-region fitting, which is getting a dedicated pass rather than a rushed one.",
        "features": [
            "One auto-cropped view per room",
            "Compiled room data book output",
            "Per-room floor plan sheets",
            "Batch generation across a whole level or project",
        ],
    },
    {
        "slug": "autoheights", "name": "AutoHeights",
        "tagline": "Auto-adjust room heights by raycasting to the nearest floor or ceiling above.",
        "category": "rooms", "priceUSD": 39, "status": "in-development", "icon": "📏",
        "description": "Raycasts from each room upward to find the nearest floor or ceiling and sets the room's height accordingly — needs the ReferenceIntersector raycasting API used carefully to be reliable across real models, so it's not shipping until that's been tested properly.",
        "features": [
            "Raycast-based room height detection",
            "Auto-sets room height to the nearest floor/ceiling above",
            "Batch across a level or the whole project",
            "Flags rooms where no ceiling/floor was found above",
        ],
    },
    {
        "slug": "roomclearheight", "name": "RoomClearHeight",
        "tagline": "Calculate slab-to-slab clear height per room and write it to a parameter.",
        "category": "rooms", "priceUSD": 29, "status": "in-development", "icon": "📐",
        "description": "Raycasts from each room's floor to the underside of the structure above and writes the clear height to a parameter — shares the same raycasting foundation as Auto Heights, so it's tracking that tool's timeline.",
        "features": [
            "Slab-to-slab clear height calculation per room",
            "Writes the result to a chosen parameter",
            "Batch across a level or the whole project",
            "Flags rooms where the calculation is ambiguous",
        ],
    },
    {
        "slug": "pointcloudcolors", "name": "PointCloudColors",
        "tagline": "Apply color override modes to point cloud instances in the active view.",
        "category": "highlight", "priceUSD": 25, "status": "in-development", "icon": "☁️",
        "description": "Point cloud color override modes (by intensity, by scan, by elevation) need the point-cloud-specific API surface, which is used rarely enough that it needs real testing against an actual scan-linked project before shipping.",
        "features": [
            "Color override modes for linked point clouds",
            "Applies per point cloud instance",
            "Active-view scoped",
            "By-intensity, by-scan, and by-elevation modes planned",
        ],
    },
    {
        "slug": "pointcloudheatmap", "name": "PointCloudHeatmap",
        "tagline": "Analyze wall-to-scan deviation and apply a color-coded heatmap to the point cloud.",
        "category": "highlight", "priceUSD": 45, "status": "in-development", "icon": "🌡️",
        "description": "Comparing as-built scan data to modeled walls and rendering the deviation as a heatmap is a scan-to-BIM analysis feature in its own right — real value, but real geometry-comparison work that deserves more than a guessed-at implementation.",
        "features": [
            "Wall-to-scan deviation analysis",
            "Color-coded heatmap override on the point cloud",
            "Per-wall deviation report",
            "Configurable deviation tolerance bands",
        ],
    },
]

with open(CATALOG_PATH) as f:
    catalog = json.load(f)

existing_category_ids = {c["id"] for c in catalog["categories"]}
for cat in NEW_CATEGORIES:
    if cat["id"] not in existing_category_ids:
        catalog["categories"].append(cat)

next_id = max(p["id"] for p in catalog["plugins"]) + 1
existing_slugs = {p["slug"] for p in catalog["plugins"]}

added = 0
for plugin in NEW_PLUGINS:
    if plugin["slug"] in existing_slugs:
        print(f"Skipping duplicate slug: {plugin['slug']}")
        continue
    entry = {"id": next_id, **plugin}
    # reorder keys to match existing schema convention
    ordered = {
        "id": entry["id"], "slug": entry["slug"], "name": entry["name"],
        "tagline": entry["tagline"], "category": entry["category"],
        "priceUSD": entry["priceUSD"], "status": entry["status"], "icon": entry["icon"],
        "description": entry["description"], "features": entry["features"],
    }
    catalog["plugins"].append(ordered)
    next_id += 1
    added += 1

with open(CATALOG_PATH, "w") as f:
    json.dump(catalog, f, indent=2)
    f.write("\n")

live_count = sum(1 for p in catalog["plugins"] if p["status"] == "live")
print(f"Added {added} plugins. Catalog now has {len(catalog['plugins'])} total, {live_count} live, {len(catalog['categories'])} categories.")
