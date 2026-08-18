#!/usr/bin/env python3
"""Adds plugins inspired by NexionTools' QC, Manage, and Data tool
categories — cross-referenced against the existing catalog first (Warning
Triage -> WarningResolver, Growth Tracker -> ModelHealthDashboard, and
Family Finder-style auditing -> FamilyAuditor already existed) so this only
adds the genuine gaps. No new categories needed; everything fits the
existing 12."""
import json
import os

CATALOG_PATH = os.path.join(os.path.dirname(__file__), "..", "..", "data", "plugins.json")

NEW_PLUGINS = [
    {
        "slug": "qcsummary", "name": "QCSummary",
        "tagline": "One-click QA dashboard: unbounded rooms, disconnected walls, orphaned doors, sill consistency.",
        "category": "qaqc-cleanup", "priceUSD": 45, "status": "live", "icon": "🩺",
        "description": "Runs a set of model-wide sanity checks in one pass — unbounded/unenclosed rooms, zero-length or unconnected walls, doors and windows whose host was deleted out from under them, forbidden categories you flag, and window sill heights that don't match the rest of their level — and reports every hit in one table.",
        "features": [
            "Unbounded/unenclosed room detection",
            "Zero-length and disconnected wall detection",
            "Doors/windows with a missing host",
            "User-configurable forbidden-category list",
            "Sill-height consistency check per level",
        ],
    },
    {
        "slug": "overriddendimensions", "name": "OverriddenDimensions",
        "tagline": "Find dimensions with a manually typed value override.",
        "category": "qaqc-cleanup", "priceUSD": 19, "status": "live", "icon": "📏",
        "description": "Scans every dimension in the model for a manual value override on any segment — the kind of thing that quietly makes a drawing say something the model doesn't, and is easy to miss scrolling through sheets by eye.",
        "features": [
            "Model-wide scan for dimension value overrides",
            "Works on both single and multi-segment dimensions",
            "Jump-to-element from the results list",
            "One pass, no configuration needed",
        ],
    },
    {
        "slug": "elementxray", "name": "ElementX-Ray",
        "tagline": "Diagnose why a selected element isn't visible in the active view.",
        "category": "qaqc-cleanup", "priceUSD": 39, "status": "live", "icon": "🩻",
        "description": "Pick an element that should be visible but isn't, and ElementX-Ray checks category visibility, direct element hide state, workset visibility, view template, phase, view filters, crop region, and (on plan views) view range — then offers a one-click fix for the checks that are safe to auto-correct.",
        "features": [
            "Eight visibility checks in one pass",
            "Category, hide-state, and workset auto-fix where it's safe to apply directly",
            "View template, phase, filter, crop, and view-range are reported (not auto-changed, since fixing those changes shared view settings)",
            "Works from a single element pick",
        ],
    },
    {
        "slug": "batchscheduleexporter", "name": "BatchScheduleExporter",
        "tagline": "Export every schedule in the project to individual CSV files in one pass.",
        "category": "data-interop", "priceUSD": 19, "status": "live", "icon": "📤",
        "description": "Runs Revit's own schedule export against every schedule view in the project and writes each one to its own CSV in a folder you pick — the batch version of the export Excel2Revit already does one schedule at a time.",
        "features": [
            "Exports every schedule view in the project",
            "One CSV per schedule, named after the schedule",
            "Uses Revit's native schedule export under the hood",
            "Success/failure summary when it's done",
        ],
    },
    {
        "slug": "familyloaderpro", "name": "FamilyLoaderPro",
        "tagline": "Batch-load every family in a folder, with conflict handling.",
        "category": "families-parameters", "priceUSD": 29, "status": "live", "icon": "📥",
        "description": "Point it at a folder and it loads every .rfa in it in one pass, reporting exactly which families loaded clean, which already existed and were overwritten, and which failed — instead of dragging files in one at a time and clicking through the same prompt.",
        "features": [
            "Batch-loads every .rfa file in a chosen folder",
            "Detects and reports already-loaded-family conflicts",
            "Per-file success/overwrite/failure report",
            "No per-file prompts to click through",
        ],
    },
    {
        "slug": "crossprojecttransfer", "name": "CrossProjectTransfer",
        "tagline": "Copy element types from one open Revit project into another.",
        "category": "coordination", "priceUSD": 35, "status": "live", "icon": "🔀",
        "description": "Pick a category and a set of types from any other project you have open, and copy them straight into the current one — for the standard types you keep re-loading from an old project instead of a proper template.",
        "features": [
            "Copies types between any two currently-open documents",
            "Category-scoped type picker with multi-select",
            "Per-type success/failure report",
            "Requires both projects open in the same Revit session",
        ],
    },
    {
        "slug": "materialswap", "name": "MaterialSwap",
        "tagline": "Batch-swap a material across every compound-structure layer that uses it.",
        "category": "qaqc-cleanup", "priceUSD": 25, "status": "live", "icon": "🧵",
        "description": "Pick a source and target material, and MaterialSwap finds every wall, floor, roof, and ceiling type layer assigned the source material and reassigns it — the batch alternative to opening every type's structure editor by hand.",
        "features": [
            "Scans wall, floor, roof, and ceiling type compound structures",
            "Also covers the Structural Material parameter on framing types",
            "Reports every type/layer it touched",
            "Single-transaction, fully undo-able",
        ],
    },
    {
        "slug": "familyfinder", "name": "FamilyFinder",
        "tagline": "Audit loaded families by type count vs. placed instance count to find bloat.",
        "category": "families-parameters", "priceUSD": 25, "status": "live", "icon": "🔍",
        "description": "Lists every loaded family with its type count and placed instance count side by side, and flags the families with a lot of types and few (or zero) instances placed — the ones most likely padding your file size for nothing.",
        "features": [
            "Type count and placed-instance count for every family",
            "Flags high-type, low-instance bloat candidates",
            "Sortable results table",
            "Model-wide scan in one pass",
        ],
    },
    {
        "slug": "staircalculator", "name": "StairCalculator",
        "tagline": "Check stair riser/tread proportions against the 2R+G comfort rule.",
        "category": "qaqc-cleanup", "priceUSD": 19, "status": "live", "icon": "🪜",
        "description": "Reads the actual riser height and tread depth of every stair run in the project and checks it against the well-known 2×riser + going comfort guideline (24–25 in). This is a design-proportion sanity check, not a certified code compliance review — always verify against the building code that actually applies to your project.",
        "features": [
            "Checks every stair run's actual riser height and tread depth",
            "Flags runs outside the 24–25 in 2R+G comfort band",
            "Model-wide scan in one pass",
            "Explicitly not a substitute for a real code compliance review",
        ],
    },
    {
        "slug": "unplacedviewfinder", "name": "UnplacedViewFinder",
        "tagline": "Find drawing views that aren't placed on any sheet.",
        "category": "qaqc-cleanup", "priceUSD": 19, "status": "live", "icon": "🗂️",
        "description": "Lists every floor plan, section, elevation, 3D, drafting, and detail view that isn't placed as a viewport on any sheet — the views most likely to be scratch work left behind in the project browser. Reports and lets you select them; nothing gets deleted automatically.",
        "features": [
            "Scans plan, section, elevation, 3D, drafting, and detail views",
            "Cross-references every sheet's placed viewports",
            "Report + select-in-browser, never auto-deletes",
            "Schedules and legends are intentionally excluded (often kept unplaced on purpose)",
        ],
    },
    {
        "slug": "csimaterialtakeoff", "name": "CSIMaterialTakeoff",
        "tagline": "Material takeoff routed to CSI MasterFormat divisions.",
        "category": "data-interop", "priceUSD": 45, "status": "in-development", "icon": "📑",
        "description": "Routing a material takeoff to the correct CSI MasterFormat division needs a certified mapping table, not a guessed one, and Revit already has a native Material Takeoff schedule category for the underlying quantities — so this is waiting on real CSI reference data rather than shipping a division-code mapping we can't stand behind.",
        "features": [
            "CSI MasterFormat division routing",
            "Mixed-unit quantity handling",
            "Manufacturer data fields",
            "Built on top of Revit's native material takeoff quantities",
        ],
    },
]

with open(CATALOG_PATH) as f:
    catalog = json.load(f)

next_id = max(p["id"] for p in catalog["plugins"]) + 1
existing_slugs = {p["slug"] for p in catalog["plugins"]}

added = 0
for plugin in NEW_PLUGINS:
    if plugin["slug"] in existing_slugs:
        print(f"Skipping duplicate slug: {plugin['slug']}")
        continue
    entry = {"id": next_id, **plugin}
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
