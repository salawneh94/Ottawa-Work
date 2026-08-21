; Ottawa Tools Revit Add-ins — one-click installer (Inno Setup script)
;
; Packages the CI-built plugin DLLs, .addin manifests, and branded-export
; logo from dist/OttawaTools-Addins-2025 (see .github/workflows/build-plugins.yml)
; into a single setup.exe, matching the "one-installer, many-tools" model the
; top-level README describes. Ribbon icons need no entry here — they're
; embedded resources inside OttawaWork.QuickAccessRibbon.dll itself (see
; RibbonBuilder.cs), so that one *.dll line above already carries them.
;
; Installs into the standard per-user Revit 2025 AddIns folder Revit itself
; scans on startup, so it needs no admin rights. Flat-folder layout is
; required: every .addin manifest resolves its DLL by bare filename next to
; itself, and BrandedXlsx.cs looks up its Branding subfolder relative to
; wherever OttawaWork.Shared.dll ends up — so every file here has to land
; together, unchanged from the packaged layout.
;
; Build with: ISCC.exe plugins\installer\OttawaTools.iss  (see CI workflow)
; Requires Inno Setup 6 (https://jrsoftware.org/isinfo.php).

#define MyAppName "Ottawa Tools Revit Add-ins"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Ottawa Ingenieure"
#define RevitYear "2025"
#define DistDir "..\..\dist\OttawaTools-Addins-2025"

[Setup]
AppId={{983D9F91-D71E-4FFA-8DA0-141194991704}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={userappdata}\Autodesk\Revit\Addins\{#RevitYear}
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\..\dist
OutputBaseFilename=OttawaTools-Setup-{#RevitYear}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName} ({#RevitYear})

[Files]
Source: "{#DistDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DistDir}\*.addin"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DistDir}\Branding\*.png"; DestDir: "{app}\Branding"; Flags: ignoreversion

[Messages]
FinishedLabel=%nOttawa Tools is installed. Restart Revit {#RevitYear} to see the Ottawa Tools ribbon tab.
