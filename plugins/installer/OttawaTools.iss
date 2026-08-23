; Ottawa Tools Revit Add-ins — one-click installer (Inno Setup script)
;
; Packages the CI-built plugin DLLs, .addin manifests, and branded-export
; logo from dist/OttawaTools-Addins-<RevitYear> (see
; .github/workflows/build-plugins.yml, which builds this once per Revit
; year in its matrix) into a single setup.exe, matching the
; "one-installer, many-tools" model the top-level README describes.
; Ribbon icons need no entry here — they're embedded resources inside
; OttawaWork.QuickAccessRibbon.dll itself (see RibbonBuilder.cs), so that
; one *.dll line above already carries them.
;
; Installs into the standard per-user Revit AddIns folder for the target
; year that Revit itself scans on startup, so it needs no admin rights.
; Flat-folder layout is required: every .addin manifest resolves its DLL
; by bare filename next to itself, and BrandedXlsx.cs looks up its
; Branding subfolder relative to wherever OttawaWork.Shared.dll ends up —
; so every file here has to land together, unchanged from the packaged
; layout.
;
; Build with: ISCC.exe plugins\installer\OttawaTools.iss  (defaults to 2025)
;         or: ISCC.exe plugins\installer\OttawaTools.iss /DRevitYear=2026
; Requires Inno Setup 6 (https://jrsoftware.org/isinfo.php).

#ifndef RevitYear
  #define RevitYear "2025"
#endif
#define DistDir "..\..\dist\OttawaTools-Addins-" + RevitYear

; Two editions (one per supported Revit year) install to different
; per-year AddIns folders and can coexist side by side, so each needs its
; own AppId — sharing one would make Windows treat installing the second
; edition as an upgrade/reinstall of the first, colliding their Programs
; and Features entries instead of keeping them separate.
#if RevitYear == "2026"
  #define MyAppId "{{4CB9F6C6-DBB9-4450-A9FD-D30B816C9F82}"
#else
  #define MyAppId "{{983D9F91-D71E-4FFA-8DA0-141194991704}"
#endif

#define MyAppName "Ottawa Tools Revit Add-ins"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Ottawa Ingenieure"

[Setup]
AppId={#MyAppId}
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
