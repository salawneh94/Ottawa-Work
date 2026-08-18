#!/usr/bin/env python3
"""Scaffolds the 11 plugins added in add-nexion-wave2-plugins.py: a
compiling project (csproj, .addin, Application.cs) for each. Command.cs is
written by hand afterward for the 10 that ship live this pass; the one
in-development plugin gets a placeholder like earlier waves."""
import json
import os
import re
import uuid

ROOT = os.path.join(os.path.dirname(__file__), "..", "src")

with open(os.path.join(os.path.dirname(__file__), "..", "..", "data", "plugins.json")) as f:
    CATALOG = json.load(f)

CATEGORY_NAMES = {c["id"]: c["name"] for c in CATALOG["categories"]}

NEW_SLUGS = {
    "qcsummary", "overriddendimensions", "elementxray", "batchscheduleexporter",
    "familyloaderpro", "crossprojecttransfer", "materialswap", "familyfinder",
    "staircalculator", "unplacedviewfinder", "csimaterialtakeoff",
}

PLUGINS = [p for p in CATALOG["plugins"] if p["slug"] in NEW_SLUGS]

CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>BIMFlow.{folder}</RootNamespace>
    <AssemblyName>BIMFlow.{folder}</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\\Shared\\BIMFlow.Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="{folder}.addin">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
"""

ADDIN = """<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>BIMFlow — {display}</Name>
    <Assembly>BIMFlow.{folder}.dll</Assembly>
    <AddInId>{{{guid}}}</AddInId>
    <FullClassName>BIMFlow.{folder}.Application</FullClassName>
    <VendorId>BIMFlow</VendorId>
    <VendorDescription>BIMFlow, https://bimflow.app</VendorDescription>
  </AddIn>
</RevitAddIns>
"""

APPLICATION_CS = """using BIMFlow.Shared;

namespace BIMFlow.{folder};

public class Application : BimFlowApplication
{{
    protected override string PanelName => "{panel}";
    protected override string ButtonInternalName => "{folder}Button";
    protected override string ButtonText => "{display}";
    protected override string ToolTip => "{tooltip}";
    protected override string CommandFullClassName => typeof(Command).FullName!;
}}
"""

PLACEHOLDER_COMMAND_CS = """using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;

namespace BIMFlow.{folder};

/// <summary>
/// Placeholder — {display} is still in development. Tracked in the
/// catalog (data/plugins.json) as "in-development"; not sold yet, so this
/// doesn't gate on a license.
/// </summary>
public class Command : BimFlowCommand
{{
    protected override string PluginSlug => "{slug}";
    protected override bool RequiresLicense => false;

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {{
        TaskDialog.Show("BIMFlow — {display}", "{display} is still in development. Check bimflow.app for release updates.");
        return Result.Succeeded;
    }}
}}
"""

guid_path = os.path.join(os.path.dirname(__file__), "nexion_wave2_guids.json")
guids = {p["slug"]: str(uuid.uuid4()) for p in PLUGINS}
with open(guid_path, "w") as f:
    json.dump(guids, f, indent=2)

for p in PLUGINS:
    folder = re.sub(r"[^a-zA-Z0-9]", "", p["name"])
    slug = p["slug"]
    guid = guids[slug]
    panel = CATEGORY_NAMES.get(p["category"], p["category"])
    display = p["name"]
    tooltip = p["tagline"].replace('"', "'")

    project_dir = os.path.join(ROOT, folder)
    os.makedirs(project_dir, exist_ok=True)

    with open(os.path.join(project_dir, f"BIMFlow.{folder}.csproj"), "w") as f:
        f.write(CSPROJ.format(folder=folder))

    with open(os.path.join(project_dir, f"{folder}.addin"), "w") as f:
        f.write(ADDIN.format(display=display, folder=folder, guid=guid))

    with open(os.path.join(project_dir, "Application.cs"), "w") as f:
        f.write(APPLICATION_CS.format(folder=folder, panel=panel, display=display, tooltip=tooltip))

    if p["status"] != "live":
        with open(os.path.join(project_dir, "Command.cs"), "w") as f:
            f.write(PLACEHOLDER_COMMAND_CS.format(folder=folder, slug=slug, display=display))

print(f"Scaffolded {len(PLUGINS)} plugin projects ({sum(1 for p in PLUGINS if p['status']=='live')} awaiting real Command.cs, {sum(1 for p in PLUGINS if p['status']!='live')} placeholder).")
