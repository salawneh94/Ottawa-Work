#!/usr/bin/env python3
"""One-time generator for the boilerplate (csproj, .addin, Application.cs) of
each first-wave BIMFlow plugin. Command.cs (the actual logic) is written by
hand for each plugin, not generated here."""
import json
import os

ROOT = os.path.join(os.path.dirname(__file__), "..", "src")

with open(os.path.join(os.path.dirname(__file__), "live_guids.json")) as f:
    GUIDS = json.load(f)

# folder, namespace, addin display name, panel (category), button text, tooltip, slug
PLUGINS = [
    ("SheetListExporter", "sheetlistexporter", "Sheets & Documentation", "Sheet List",
     "Export or re-import the sheet list to/from CSV.", ),
    ("ViewFilterCopy", "viewfiltercopy", "Views & Annotation", "Copy Filters",
     "Copy view filters and overrides from the active view to other views."),
    ("CropSync", "cropsync", "Views & Annotation", "Crop Sync",
     "Copy the crop region and view range from the active view to other views."),
    ("GridBubbleManager", "gridbubblemanager", "Views & Annotation", "Grid Bubbles",
     "Batch toggle grid and level bubble visibility."),
    ("ScopeBoxSync", "scopeboxsync", "Views & Annotation", "Scope Box Sync",
     "Apply a scope box to a batch of selected views."),
    ("TextStyleAudit", "textstyleaudit", "Views & Annotation", "Text Style Audit",
     "Scan the model for text and dimension styles outside the approved list."),
    ("PurgePro", "purgepro", "Model QA/QC & Cleanup", "Purge Pro",
     "Repeat Revit's purge-unused until the model stops shrinking."),
    ("DuplicateFinder", "duplicatefinder", "Model QA/QC & Cleanup", "Duplicate Finder",
     "Find overlapping or duplicate elements in the model."),
    ("WallJoinFixer", "walljoinfixer", "Model QA/QC & Cleanup", "Wall Join Fixer",
     "Batch-set wall join types and re-join nearby wall geometry."),
    ("SharedParamSync", "sharedparamsync", "Families & Parameters", "Shared Param Sync",
     "Diff and merge two shared parameter files."),
    ("RoomRenumber", "roomrenumber", "Renumbering & Batch Ops", "Room Renumber",
     "Renumber rooms by direction of travel or zone."),
    ("DoorWindowRenumber", "doorwindowrenumber", "Renumbering & Batch Ops", "Door/Window Renumber",
     "Renumber doors or windows with a configurable scheme."),
    ("GridRenumber", "gridrenumber", "Renumbering & Batch Ops", "Grid Renumber",
     "Batch rename grids or levels without breaking references."),
    ("ViewRenamer", "viewrenamer", "Renumbering & Batch Ops", "View Renamer",
     "Batch rename views with find/replace, prefix/suffix, or regex."),
    ("QuickSelect", "quickselectplus", "Productivity", "QuickSelect+",
     "Select elements by category and parameter-value rules."),
    ("BatchExporter", "batchexporter", "Productivity", "Batch Exporter",
     "Export sheets or views to PDF, DWG, or NWC with a naming template."),
]

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

for folder, slug, panel, display, tooltip in PLUGINS:
    guid = GUIDS[slug]
    project_dir = os.path.join(ROOT, folder)
    os.makedirs(project_dir, exist_ok=True)

    with open(os.path.join(project_dir, f"BIMFlow.{folder}.csproj"), "w") as f:
        f.write(CSPROJ.format(folder=folder))

    with open(os.path.join(project_dir, f"{folder}.addin"), "w") as f:
        f.write(ADDIN.format(display=display, folder=folder, guid=guid))

    with open(os.path.join(project_dir, "Application.cs"), "w") as f:
        f.write(APPLICATION_CS.format(folder=folder, panel=panel, display=display, tooltip=tooltip))

print(f"Scaffolded {len(PLUGINS)} plugin projects.")
