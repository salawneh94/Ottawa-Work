# Ottawa Tools Revit Plugins

Internal Revit add-ins for the firm's own use — a pruned fork of the BIMFlow
catalog (see Status below) with no license gate and no website integration.
One shared "Ottawa Tools" ribbon tab, one add-in per plugin.

## Structure

```
plugins/
  OttawaWork.sln           Solution referencing every project below
  Directory.Build.props    Shared build settings + Revit API references
  src/
    Shared/                Common SDK: ribbon registration, shared dialogs
    <PluginName>/           One folder per wired plugin (see OttawaRoster.cs)
      OttawaWork.<PluginName>.csproj
      <PluginName>.addin    Revit add-in manifest
      Application.cs        Ribbon registration (IExternalApplication)
      Command.cs             The actual tool logic (IExternalCommand)
  scripts/                  One-off generators from the pre-fork catalog era (unused now)
```

Every plugin folder name is derived from the catalog's `name` field with
non-alphanumeric characters stripped (see `pluginProjectFolder()` in
`web/src/lib/catalog.ts`) — the website's download endpoint zips this exact
folder, so the two stay in sync automatically.

Each plugin's own `Application.cs` still declares its panel/text/tooltip/
command as a record of what it is, but doesn't register a ribbon button
itself (`OttawaWorkApplication.OnStartup` is a no-op) — the whole ribbon is
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

Ottawa-Work is a pruned, internal fork — it only keeps source for the
**37 plugins actually wired into the Ottawa Tools ribbon**
(`src/QuickAccessRibbon/OttawaRoster.cs` is the single source of truth for
exactly which ones and what panel each sits in). The sibling BIMFlow repo
carries the full 75+ plugin marketing catalog (`data/plugins.json`, sold
per-plugin on the site) that this one doesn't have and doesn't need —
Ottawa-Work has no license gate, no website, and no plugins beyond what the
firm's own ribbon actually shows.

An earlier version of this repo carried the sibling catalog's full plugin
source wholesale, with only the ribbon wiring curated down to 37 — the
other ~58 never appeared anywhere in the UI (`OttawaWorkApplication.OnStartup`
is a no-op for every plugin; only `OttawaRoster.Entries` puts a button on
the ribbon) but still compiled on every CI run and still shipped their
`.dll`/`.addin` into the installer, loading into Revit at startup for no
visible benefit. Those were removed entirely (still in git history if a
future pass wants any of them back) rather than left as dead weight.

## Building

`.github/workflows/build-plugins.yml` builds `OttawaWork.sln` on a
windows-latest GitHub Actions runner on every push/PR touching `plugins/` —
so unlike most of this project's history, this isn't unverified: all 37
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

- `ottawatools-installer-2025` — `OttawaTools-Setup-2025.exe`
  ([Inno Setup](https://jrsoftware.org/isinfo.php) script at
  `plugins/installer/OttawaTools.iss`). Run it, restart Revit, done — installs
  per-user, no admin rights needed.
- `ottawatools-addins-2025` — the same files unpacked into a folder (every
  plugin's DLL and `.addin` manifest already flattened together), for
  dropping straight into Revit's Addins folder by hand if you'd rather not
  run an installer.

Either way, useful for functional-testing the add-ins without installing
the .NET SDK or Visual Studio at all.

To build it yourself instead:

1. Install Revit (2025 by default; see below for older versions) if you want
   to actually run the add-ins — it's not required just to build.
2. Open `OttawaWork.sln`.
3. Build. Each project's `.addin` manifest is copied next to its output DLL.
4. Each `.addin` manifest references its own DLL by bare filename (resolved
   relative to the manifest's own folder), so copy every plugin's output
   DLL and `.addin` file — flattened into one folder, not kept in separate
   per-plugin subfolders — into `%AppData%\Autodesk\Revit\Addins\<version>\`.
5. Ribbon icons need no separate copy step — they're embedded resources
   inside `OttawaWork.QuickAccessRibbon.dll` itself
   (`plugins/src/QuickAccessRibbon/Resources/Icons/3d`), loaded via a
   `pack://application:,,,/OttawaWork.QuickAccessRibbon;component/...` URI by
   `RibbonBuilder.ApplyIcon`. Building and copying that one DLL (step 4) is
   enough; see the file for why.

### Targeting an older Revit version

`Directory.Build.props` defaults to Revit 2025 (.NET 8). For Revit 2022–2024
(.NET Framework 4.8), build with:

```
dotnet build -p:RevitVersion=2024
```

(or set `RevitVersion` in a specific project file) — this also selects the
matching `Nice3point.Revit.Api.RevitAPI`/`RevitAPIUI` package version.

## Licensing

None — the licensing/website-integration layer (`LicenseClient.cs`,
`LicenseActivationDialog.cs`) that the sibling BIMFlow catalog uses was
stripped from this fork entirely. Every tool here runs unconditionally,
with no license check and no network call.
