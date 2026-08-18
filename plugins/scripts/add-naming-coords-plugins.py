#!/usr/bin/env python3
"""Adds two plugins requested directly by name: NamingConventionAudit and
SharedCoordinatesAudit — both real BIM-manager pain points not covered by
the existing catalog (StandardsChecker is rule-based on parameters, not
name patterns; LinkedModelAuditor covers load/pin/workset status, not
coordinate alignment)."""
import json
import os

CATALOG_PATH = os.path.join(os.path.dirname(__file__), "..", "..", "data", "plugins.json")

NEW_PLUGINS = [
    {
        "slug": "namingconventionaudit", "name": "NamingConventionAudit",
        "tagline": "Check view, sheet, or family type names against a regex pattern you define.",
        "category": "qaqc-cleanup", "priceUSD": 25, "status": "live", "icon": "🔠",
        "description": "Pick views, sheets, or family types, type the regex pattern your BEP's naming standard actually follows, and it lists every name that doesn't match — the general-purpose naming check that isn't locked to one category or one hardcoded scheme.",
        "features": [
            "Works on views, sheets, or family types",
            "Any regex pattern — no hardcoded naming scheme",
            "Lists every non-matching name with a jump-to-element action",
            "Model-wide scan in one pass",
        ],
    },
    {
        "slug": "sharedcoordinatesaudit", "name": "SharedCoordinatesAudit",
        "tagline": "Check whether every linked model's survey point actually lines up with this project's shared coordinates.",
        "category": "coordination", "priceUSD": 35, "status": "live", "icon": "🧭",
        "description": "For every loaded link, transforms its survey point into this project's coordinate system and compares it to this project's own survey point — the same real-world monument should land in the same place if the link was positioned by shared coordinates rather than dragged or origin-to-origin placed. Also flags a True North mismatch where that parameter is readable. This checks alignment, not which positioning mode Revit used to place the link (that isn't exposed after the fact).",
        "features": [
            "Per-link survey point alignment check against the host project",
            "True North angle comparison where readable",
            "Flags unloaded links instead of silently skipping them",
            "Model-wide scan in one pass",
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
