#!/usr/bin/env python3
"""Removes every plugin whose status isn't "live" from the catalog, and
prints the project folder name for each so the caller can delete
plugins/src/<Folder> for each one. Verified beforehand that no category
goes to zero live plugins and no bundle references a removed slug by name
(the one category-scoped bundle, starter-bundle, already only advertises
the live count)."""
import json
import os
import re

CATALOG_PATH = os.path.join(os.path.dirname(__file__), "..", "..", "data", "plugins.json")

with open(CATALOG_PATH) as f:
    catalog = json.load(f)

removed = [p for p in catalog["plugins"] if p["status"] != "live"]
catalog["plugins"] = [p for p in catalog["plugins"] if p["status"] == "live"]

with open(CATALOG_PATH, "w") as f:
    json.dump(catalog, f, indent=2)
    f.write("\n")

print(f"Removed {len(removed)} plugin(s). Catalog now has {len(catalog['plugins'])} plugins, all live.")
print()
print("Folders to delete:")
for p in removed:
    folder = re.sub(r"[^a-zA-Z0-9]", "", p["name"])
    print(folder)
