#!/usr/bin/env python3
"""Generates BIMFlow.sln listing every project under /plugins/src."""
import os
import uuid

SRC = os.path.join(os.path.dirname(__file__), "..", "src")
SLN_PATH = os.path.join(os.path.dirname(__file__), "..", "BIMFlow.sln")
CSHARP_PROJECT_TYPE_GUID = "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC"

projects = []
for folder in sorted(os.listdir(SRC)):
    folder_path = os.path.join(SRC, folder)
    if not os.path.isdir(folder_path):
        continue
    csproj = next((f for f in os.listdir(folder_path) if f.endswith(".csproj")), None)
    if not csproj:
        continue
    name = csproj[:-len(".csproj")]
    rel_path = f"src\\{folder}\\{csproj}"
    projects.append((name, rel_path, str(uuid.uuid4()).upper()))

lines = []
lines.append("Microsoft Visual Studio Solution File, Format Version 12.00")
lines.append("# Visual Studio Version 17")
lines.append("VisualStudioVersion = 17.0.31903.59")
lines.append("MinimumVisualStudioVersion = 10.0.40219.1")

for name, rel_path, guid in projects:
    lines.append(f'Project("{{{CSHARP_PROJECT_TYPE_GUID}}}") = "{name}", "{rel_path}", "{{{guid}}}"')
    lines.append("EndProject")

lines.append("Global")
lines.append("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution")
lines.append("\t\tDebug|x64 = Debug|x64")
lines.append("\t\tRelease|x64 = Release|x64")
lines.append("\tEndGlobalSection")
lines.append("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution")
for _, _, guid in projects:
    lines.append(f"\t\t{{{guid}}}.Debug|x64.ActiveCfg = Debug|x64")
    lines.append(f"\t\t{{{guid}}}.Debug|x64.Build.0 = Debug|x64")
    lines.append(f"\t\t{{{guid}}}.Release|x64.ActiveCfg = Release|x64")
    lines.append(f"\t\t{{{guid}}}.Release|x64.Build.0 = Release|x64")
lines.append("\tEndGlobalSection")
lines.append("\tGlobalSection(SolutionProperties) = preSolution")
lines.append("\t\tHideSolutionNode = FALSE")
lines.append("\tEndGlobalSection")
lines.append("EndGlobal")
lines.append("")

with open(SLN_PATH, "w", newline="\r\n") as f:
    f.write("\n".join(lines))

print(f"Wrote {SLN_PATH} with {len(projects)} projects.")
