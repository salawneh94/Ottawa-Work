#!/usr/bin/env python3
"""Scaffolds the 34 catalog plugins still marked "in-development": a
compiling project (csproj, .addin, Application.cs) plus a placeholder
Command.cs that tells the user the tool is on its way, so the repo
structure matches the full 50-plugin catalog while the real logic for
each one is implemented incrementally."""
import json
import os
import re

ROOT = os.path.join(os.path.dirname(__file__), "..", "src")

with open(os.path.join(os.path.dirname(__file__), "indev_guids.json")) as f:
    GUIDS = json.load(f)

with open(os.path.join(os.path.dirname(__file__), "..", "..", "data", "plugins.json")) as f:
    CATALOG = json.load(f)

CATEGORY_NAMES = {c["id"]: c["name"] for c in CATALOG["categories"]}

PLUGINS = [p for p in CATALOG["plugins"] if p["status"] != "live"]

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

COMMAND_CS = """using Autodesk.Revit.DB;
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

for p in PLUGINS:
    folder = re.sub(r"[^a-zA-Z0-9]", "", p["name"])
    slug = p["slug"]
    guid = GUIDS[slug]
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

    with open(os.path.join(project_dir, "Command.cs"), "w") as f:
        f.write(COMMAND_CS.format(folder=folder, slug=slug, display=display))

print(f"Scaffolded {len(PLUGINS)} in-development plugin projects.")
