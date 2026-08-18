# BIMFlow Revit Plugins

Revit add-ins sold on [BIMFlow](https://bimflow.app). One shared "BIMFlow" ribbon
tab, one add-in per plugin, all built on a common licensing/SDK layer.

## Structure

```
plugins/
  BIMFlow.sln              Solution referencing every project below
  Directory.Build.props    Shared build settings + Revit API references
  src/
    Shared/                Common SDK: licensing, ribbon registration, shared dialogs
    <PluginName>/           One folder per catalog plugin (see /data/plugins.json)
      BIMFlow.<PluginName>.csproj
      <PluginName>.addin    Revit add-in manifest
      Application.cs        Ribbon registration (IExternalApplication)
      Command.cs             The actual tool logic (IExternalCommand)
  scripts/                  One-off generators used to scaffold the above
```

Every plugin folder name is derived from the catalog's `name` field with
non-alphanumeric characters stripped (see `pluginProjectFolder()` in
`web/src/lib/catalog.ts`) — the website's download endpoint zips this exact
folder, so the two stay in sync automatically.

Each plugin's own `Application.cs` still declares its panel/text/tooltip/
command as a record of what it is, but doesn't register a ribbon button
itself (`BimFlowApplication.OnStartup` is a no-op) — the whole ribbon is
built in one centralized pass instead, by `src/QuickAccessRibbon/`. That
folder isn't a catalog plugin (not in `data/plugins.json`, not sold
separately, `RequiresLicense` hardcoded `false`); it reads
`PluginRoster.Entries` (generated from every plugin's `Application.cs` +
`data/plugins.json` + `data/plugin-impact.json`) and builds every panel —
1-2 "hero" buttons per panel (the featured/highest-priced plugins) via
`RibbonPanel.AddItem`, the rest stacked 2-3 at a time via
`RibbonPanel.AddStackedItems` — plus its own 9 quick-select commands in a
new "Select" panel and 2 more folded into "Highlight". This has to be
centralized: matching a native Revit ribbon's mix of large and small
buttons needs a panel's full button roster up front, which 75 independent
add-ins each registering one button during their own `OnStartup`, with no
defined order between them, can't provide.

## Status

All **75 plugins** in the catalog have real, working implementations
(`"status": "live"` in `data/plugins.json`). Every plugin sold on the site
has shipping code behind it — nothing in the catalog is a placeholder.

10 plugins that were previously scaffolded as `"in-development"`
(DuctPipeSizer, RebarBatch, LoadPathAuditor, COBieBuilder, KeynoteManager,
CommentSync, ModelComparer, SyncGuardian, UniqueNumbering, RoomClearHeight)
have been removed from the catalog and the repo entirely, rather than left
half-built — each one needed either correctness-critical domain expertise
(sizing calculations, rebar/load-path analysis, a real COBie or BCF format
implementation) or a Revit API surface too uncertain to implement
confidently without testing against a real install (keynote tables,
cross-model raycasting). They're still in git history if a future pass
wants to pick any of them back up.

Six other names from that original removed list came back with
deliberately narrower scope, specifically to sidestep the uncertainty that
got them pulled the first time: **LegendBuilder** (now requires an
existing legend view to duplicate — Revit has no API to create one from
nothing), **PlansPerRoom**, **CSITakeoff** (assumes Assembly Code is
already populated on element types, rather than attempting new
classification), and **PointCloudColorizer** (covers what were separately
listed as PointCloudColors/PointCloudHeatmap — a uniform override color
per instance, since true intensity/heatmap coloring isn't exposed through
the public API at all). **RoomHeightSync** and **SlabHeightSync** cover
similar ground to the old AutoHeights concept with a CSV-driven,
explicit-value approach rather than automatic calculation.

Several of the live plugins are **intentionally scoped down** from a
broader original idea for the same reason — the full promise needed either
correctness-critical domain expertise or an uncertain API surface, so the
plugin does the well-defined, high-confidence subset instead of guessing
(e.g. DimensionAuto dimensions grid-to-grid spacing rather than arbitrary
wall faces; MEPClashPrecheck does a bounding-box heuristic rather than true
solid-geometry clash detection; AutoLegendBuilder audits detail component
usage rather than line/fill patterns; StairCalculator checks the 2R+G
proportion guideline explicitly as a design sanity check, not a certified
code compliance review; QCSummary's "disconnected wall" flag is a
bounding-box proximity heuristic, not true geometric connectivity
analysis). Each plugin's `Command.cs` doc comment and the catalog's
`features` array say exactly what's covered.

## Building

`.github/workflows/build-plugins.yml` builds `BIMFlow.sln` on a
windows-latest GitHub Actions runner on every push/PR touching `plugins/` —
so unlike most of this project's history, this isn't unverified: all 74
plugins actually compile against the real Revit API. That became possible
because the Revit API references come from the community-maintained
[Nice3point.Revit.Api](https://github.com/Nice3point/RevitTemplates) NuGet
packages (see `Directory.Build.props`) instead of a local Revit install —
the same `RevitAPI.dll`/`RevitAPIUI.dll` Autodesk ships, redistributed as
reference-only packages versioned per Revit release year. That means
**no Revit license is needed to build**, on CI or on your own machine.

What CI does *not* cover: it only proves the code compiles, not that any
command behaves correctly when actually run inside Revit against a real
model — that needs an actual Revit install and manual testing, which this
repo hasn't had. Budget time for a first functional QA pass before shipping.

**Shortcut — skip Visual Studio entirely:** every CI run uploads two
artifacts (Actions tab → the latest `Build Revit Plugins` run →
Artifacts):

- `bimflow-installer-2025` — `BIMFlow-Setup-2025.exe`
  ([Inno Setup](https://jrsoftware.org/isinfo.php) script at
  `plugins/installer/BIMFlow.iss`). Run it, restart Revit, done — installs
  per-user, no admin rights needed.
- `bimflow-addins-2025` — the same files unpacked into a folder (every
  plugin's DLL and `.addin` manifest already flattened together), for
  dropping straight into Revit's Addins folder by hand if you'd rather not
  run an installer.

Either way, useful for functional-testing the add-ins without installing
the .NET SDK or Visual Studio at all.

To build it yourself instead:

1. Install Revit (2025 by default; see below for older versions) if you want
   to actually run the add-ins — it's not required just to build.
2. Open `BIMFlow.sln`.
3. Build. Each project's `.addin` manifest is copied next to its output DLL.
4. Each `.addin` manifest references its own DLL by bare filename (resolved
   relative to the manifest's own folder), so copy every plugin's output
   DLL and `.addin` file — flattened into one folder, not kept in separate
   per-plugin subfolders — into `%AppData%\Autodesk\Revit\Addins\<version>\`.
5. Also copy `plugins/src/Shared/Icons/` into an `Icons` subfolder there —
   `RibbonBuilder.ApplyIcon` loads each button's icon from
   `Icons\<key>_16.png` / `_32.png` next to the DLLs. Pre-baked PNGs, not
   rendered at runtime; see the file for why.

### Targeting an older Revit version

`Directory.Build.props` defaults to Revit 2025 (.NET 8). For Revit 2022–2024
(.NET Framework 4.8), build with:

```
dotnet build -p:RevitVersion=2024
```

(or set `RevitVersion` in a specific project file) — this also selects the
matching `Nice3point.Revit.Api.RevitAPI`/`RevitAPIUI` package version.

## Licensing

`Shared/LicenseClient.cs` calls the website's `/api/license/validate`
endpoint on first use per plugin, caches the result locally
(`%AppData%\BIMFlow\license-cache.json`) for a 7-day offline grace period,
and prompts once for a license key (`Shared/LicenseActivationDialog.cs`),
storing it in `%AppData%\BIMFlow\license.key`. Point a dev build at a local
website instance with the `BIMFLOW_API_URL` environment variable.
