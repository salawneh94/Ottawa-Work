# Revit Plugin Lists — BIMFlow Catalog Reference

Pulled from the BIMFlow plugin catalog (`data/plugins.json`) for reference only — the BIMFlow repo (`salawneh94/BIMFlow`, branch `claude/revit-plugins-marketplace-rwe3qv`) is **not** edited here. This is a separate, standalone copy of the plugin info, split into the two lists below.

## 1. Full Catalog — For the Website

All 73 plugins across 12 categories, exactly as listed for sale on the BIMFlow storefront (MEP included).


### Sheets & Documentation (8)

- **LegendBuilder** — $39 — Auto-build a legend view from every detail component and annotation symbol actually used in the model.
- **LegendPlacer** — $25 — Place a legend on multiple sheets at a consistent position.
- **LegendUsageAudit** — $35 — Audit which line styles, fill patterns, and detail components are actually used — and how often.
- **RevisionTrack** — $49 — Automate revision clouds and revision schedules across sheet sets.
- **SheetForge** — $59 — Batch-create sheets from an Excel list in one pass.
- **SheetListExporter** — $29 — Two-way sheet list sync between Revit and Excel.
- **TitleBlockUpdater** — $39 — Batch-update title block info across sheets and linked projects.
- **ViewSync** — $39 — Reassign view templates across hundreds of views at once.

### Views & Annotation (7)

- **CropSync** — $25 — Copy crop region and view range from the active view to a batch of views.
- **DimensionAuto** — $59 — Auto-place dimension strings on walls, grids, and openings.
- **GridBubbleManager** — $19 — Batch toggle grid and level bubble visibility per sheet standards.
- **ScopeBoxSync** — $19 — Apply one scope box to many views in a single action.
- **TagAll+** — $45 — Smart batch tagging with collision avoidance.
- **TextStyleAudit** — $29 — Find every off-standard text and dimension style in the model.
- **ViewFilterCopy** — $25 — Copy view filters and graphic overrides from one view to many.

### Model QA/QC & Cleanup (16)

- **DuplicateFinder** — $35 — Detect duplicate and overlapping elements before they cause problems.
- **ElementX-Ray** — $39 — Diagnose why a selected element isn't visible in the active view.
- **MaterialSwap** — $25 — Batch-swap a material across every compound-structure layer that uses it.
- **ModelHealthDashboard** — $39 — Track file size, warning count, and element count trends over time.
- **NamingConventionAudit** — $25 — Check view, sheet, or family type names against a regex pattern you define.
- **OverriddenDimensions** — $19 — Find dimensions with a manually typed value override.
- **PurgePro** — $29 — Deeper purge of unused families, materials, and view templates — with a report.
- **QCSummary** — $45 — One-click QA dashboard: unbounded rooms, disconnected walls, orphaned doors, sill consistency.
- **RoomFinisher** — $25 — Find unplaced, unenclosed, and unbounded rooms before they bite you.
- **ScheduleBlankDetector** — $25 — Find blank fields in a schedule and score how complete it is.
- **StairCalculator** — $19 — Check stair riser/tread proportions against the 2R+G comfort rule.
- **StandardsChecker** — $69 — Rule-based QA/QC audit against your firm's BIM standard.
- **UnplacedViewFinder** — $19 — Find drawing views that aren't placed on any sheet.
- **ViewMatrix** — $29 — Audit scope box and crop region settings across every view.
- **WallJoinFixer** — $19 — Batch-fix wall join conditions across the whole model.
- **WarningResolver** — $49 — Triage and batch-fix common Revit warning categories.

### Families & Parameters (8)

- **FamilyAuditor** — $39 — Scan families for best-practice issues before they ship.
- **FamilyFinder** — $25 — Audit loaded families by type count vs. placed instance count to find bloat.
- **FamilyLoaderPro** — $29 — Batch-load every family in a folder, with conflict handling.
- **FamilyTypeManager** — $35 — Bulk rename, reorganize, and purge unused family types.
- **ParamBatchEditor** — $49 — Bulk-edit parameter values in a spreadsheet-style grid.
- **ParameterFormulaPropagator** — $29 — Generate parameter values from a template pattern and sequential numbering.
- **ParameterMapper** — $39 — Transfer parameter values between related elements by rule.
- **SharedParamSync** — $29 — Diff and merge shared parameter files across projects.

### MEP (5)

- **ConnectorAlign** — $49 — Detect and fix misaligned or disconnected MEP connectors.
- **EquipmentScheduler** — $45 — Generate equipment schedules linked to cut-sheet data.
- **InsulationManager** — $29 — Batch-apply and update pipe/duct insulation by system rule.
- **MEPClashPrecheck** — $55 — Lightweight interference check between MEP disciplines before formal coordination.
- **SystemColorCoder** — $35 — Auto color-code MEP systems by type and zone across every view.

### Structural (2)

- **FramingChecker** — $45 — Validate framing member connections, orientation, and cuts.
- **GridGenerator** — $29 — Auto-generate structural grids from CAD or Excel input.

### Data & Interoperability (6)

- **BatchScheduleExporter** — $19 — Export every schedule in the project to individual CSV files in one pass.
- **CSITakeoff** — $49 — Quantity takeoff grouped by Assembly Code, exported to CSV.
- **Excel2Revit** — $49 — Two-way sync between Excel/CSV and Revit parameters or schedules.
- **ExcelAsDraft** — $35 — Import a CSV as a visual grid table in a drafting view, with one-click refresh.
- **IFCExportQA** — $45 — Pre-flight checks and mapping presets for clean IFC exports.
- **LinkedModelAuditor** — $29 — One dashboard for every linked model's status, worksets, and positioning.

### Coordination & Collaboration (3)

- **CrossProjectTransfer** — $35 — Copy element types from one open Revit project into another.
- **SharedCoordinatesAudit** — $35 — Check whether every linked model's survey point actually lines up with this project's shared coordinates.
- **WorksetMonitor** — $35 — Live dashboard of workset ownership, sync times, and element counts.

### Renumbering & Batch Ops (4)

- **DoorWindowRenumber** — $25 — Batch renumber doors and windows with a configurable scheme.
- **GridRenumber** — $19 — Batch renumber grids and levels without breaking references.
- **RoomRenumber** — $25 — Renumber rooms by floor, direction, or custom zone rules.
- **ViewRenamer** — $19 — Batch rename views with find/replace, prefix/suffix, and regex.

### Productivity (4)

- **BatchExporter** — $35 — Batch export sheets and views to PDF or DWG with a naming template.
- **QuickSelect+** — $25 — Select elements by parameter value, category, and rule combinations.
- **SelectByCategory** — $15 — One-click select every element of a category, in the view or the whole project.
- **SelectExteriorInterior** — $25 — Select every exterior or interior wall, door, or window in one click.

### Rooms & Spatial (7)

- **ModelCeilings** — $39 — Auto-create drop ceilings from room boundaries in the active floor plan.
- **PlansPerRoom** — $45 — Generate a cropped floor plan view for every room in one pass.
- **RoomColorFillPlan** — $39 — Color-fill the active plan view by any room parameter.
- **RoomFinishSchedule** — $25 — Generate a room finish schedule and export it to CSV.
- **RoomHeightSync** — $35 — Batch-set room computation heights from a CSV.
- **RoomInventory** — $35 — List every element inside each room, with a category breakdown.
- **SlabHeightSync** — $35 — Batch-set Height Offset From Level across a set of floors.

### Highlight & Visualization (3)

- **HighlightWalls** — $19 — Toggle an interior/exterior color highlight on every wall in the active view.
- **OverrideByParam** — $29 — Color-code any category by any parameter value in the active view.
- **PointCloudColorizer** — $29 — Color-tint point cloud links so multiple scans are visually distinguishable.


## 2. Work List — For Sharing With the Team (Architects & BIM Coordination)

Same catalog minus the **MEP** category and **DimensionAuto** — 67 plugins. We're architects doing BIM coordination and don't run MEP in-house, so the 5 MEP-only tools (ConnectorAlign, EquipmentScheduler, InsulationManager, MEPClashPrecheck, SystemColorCoder) are left off this list. DimensionAuto is also dropped — it only auto-*places* new dimension strings, not what's needed (see the wanted tool below).


### Sheets & Documentation (8)

- **LegendBuilder** — $39 — Auto-build a legend view from every detail component and annotation symbol actually used in the model.
- **LegendPlacer** — $25 — Place a legend on multiple sheets at a consistent position.
- **LegendUsageAudit** — $35 — Audit which line styles, fill patterns, and detail components are actually used — and how often.
- **RevisionTrack** — $49 — Automate revision clouds and revision schedules across sheet sets.
- **SheetForge** — $59 — Batch-create sheets from an Excel list in one pass.
- **SheetListExporter** — $29 — Two-way sheet list sync between Revit and Excel.
- **TitleBlockUpdater** — $39 — Batch-update title block info across sheets and linked projects.
- **ViewSync** — $39 — Reassign view templates across hundreds of views at once.

### Views & Annotation (6)

- **CropSync** — $25 — Copy crop region and view range from the active view to a batch of views.
- **GridBubbleManager** — $19 — Batch toggle grid and level bubble visibility per sheet standards.
- **ScopeBoxSync** — $19 — Apply one scope box to many views in a single action.
- **TagAll+** — $45 — Smart batch tagging with collision avoidance.
- **TextStyleAudit** — $29 — Find every off-standard text and dimension style in the model.
- **ViewFilterCopy** — $25 — Copy view filters and graphic overrides from one view to many.

### Model QA/QC & Cleanup (16)

- **DuplicateFinder** — $35 — Detect duplicate and overlapping elements before they cause problems.
- **ElementX-Ray** — $39 — Diagnose why a selected element isn't visible in the active view.
- **MaterialSwap** — $25 — Batch-swap a material across every compound-structure layer that uses it.
- **ModelHealthDashboard** — $39 — Track file size, warning count, and element count trends over time.
- **NamingConventionAudit** — $25 — Check view, sheet, or family type names against a regex pattern you define.
- **OverriddenDimensions** — $19 — Find dimensions with a manually typed value override.
- **PurgePro** — $29 — Deeper purge of unused families, materials, and view templates — with a report.
- **QCSummary** — $45 — One-click QA dashboard: unbounded rooms, disconnected walls, orphaned doors, sill consistency.
- **RoomFinisher** — $25 — Find unplaced, unenclosed, and unbounded rooms before they bite you.
- **ScheduleBlankDetector** — $25 — Find blank fields in a schedule and score how complete it is.
- **StairCalculator** — $19 — Check stair riser/tread proportions against the 2R+G comfort rule.
- **StandardsChecker** — $69 — Rule-based QA/QC audit against your firm's BIM standard.
- **UnplacedViewFinder** — $19 — Find drawing views that aren't placed on any sheet.
- **ViewMatrix** — $29 — Audit scope box and crop region settings across every view.
- **WallJoinFixer** — $19 — Batch-fix wall join conditions across the whole model.
- **WarningResolver** — $49 — Triage and batch-fix common Revit warning categories.

### Families & Parameters (8)

- **FamilyAuditor** — $39 — Scan families for best-practice issues before they ship.
- **FamilyFinder** — $25 — Audit loaded families by type count vs. placed instance count to find bloat.
- **FamilyLoaderPro** — $29 — Batch-load every family in a folder, with conflict handling.
- **FamilyTypeManager** — $35 — Bulk rename, reorganize, and purge unused family types.
- **ParamBatchEditor** — $49 — Bulk-edit parameter values in a spreadsheet-style grid.
- **ParameterFormulaPropagator** — $29 — Generate parameter values from a template pattern and sequential numbering.
- **ParameterMapper** — $39 — Transfer parameter values between related elements by rule.
- **SharedParamSync** — $29 — Diff and merge shared parameter files across projects.

### Structural (2)

- **FramingChecker** — $45 — Validate framing member connections, orientation, and cuts.
- **GridGenerator** — $29 — Auto-generate structural grids from CAD or Excel input.

### Data & Interoperability (6)

- **BatchScheduleExporter** — $19 — Export every schedule in the project to individual CSV files in one pass.
- **CSITakeoff** — $49 — Quantity takeoff grouped by Assembly Code, exported to CSV.
- **Excel2Revit** — $49 — Two-way sync between Excel/CSV and Revit parameters or schedules.
- **ExcelAsDraft** — $35 — Import a CSV as a visual grid table in a drafting view, with one-click refresh.
- **IFCExportQA** — $45 — Pre-flight checks and mapping presets for clean IFC exports.
- **LinkedModelAuditor** — $29 — One dashboard for every linked model's status, worksets, and positioning.

### Coordination & Collaboration (3)

- **CrossProjectTransfer** — $35 — Copy element types from one open Revit project into another.
- **SharedCoordinatesAudit** — $35 — Check whether every linked model's survey point actually lines up with this project's shared coordinates.
- **WorksetMonitor** — $35 — Live dashboard of workset ownership, sync times, and element counts.

### Renumbering & Batch Ops (4)

- **DoorWindowRenumber** — $25 — Batch renumber doors and windows with a configurable scheme.
- **GridRenumber** — $19 — Batch renumber grids and levels without breaking references.
- **RoomRenumber** — $25 — Renumber rooms by floor, direction, or custom zone rules.
- **ViewRenamer** — $19 — Batch rename views with find/replace, prefix/suffix, and regex.

### Productivity (4)

- **BatchExporter** — $35 — Batch export sheets and views to PDF or DWG with a naming template.
- **QuickSelect+** — $25 — Select elements by parameter value, category, and rule combinations.
- **SelectByCategory** — $15 — One-click select every element of a category, in the view or the whole project.
- **SelectExteriorInterior** — $25 — Select every exterior or interior wall, door, or window in one click.

### Rooms & Spatial (7)

- **ModelCeilings** — $39 — Auto-create drop ceilings from room boundaries in the active floor plan.
- **PlansPerRoom** — $45 — Generate a cropped floor plan view for every room in one pass.
- **RoomColorFillPlan** — $39 — Color-fill the active plan view by any room parameter.
- **RoomFinishSchedule** — $25 — Generate a room finish schedule and export it to CSV.
- **RoomHeightSync** — $35 — Batch-set room computation heights from a CSV.
- **RoomInventory** — $35 — List every element inside each room, with a category breakdown.
- **SlabHeightSync** — $35 — Batch-set Height Offset From Level across a set of floors.

### Highlight & Visualization (3)

- **HighlightWalls** — $19 — Toggle an interior/exterior color highlight on every wall in the active view.
- **OverrideByParam** — $29 — Color-code any category by any parameter value in the active view.
- **PointCloudColorizer** — $29 — Color-tint point cloud links so multiple scans are visually distinguishable.

### Wanted — Not in the Catalog Yet

- **DimensionOverride** (idea) — Edit an existing dimension's displayed value directly — pick a dimension (or a batch of them) and swap its shown number for another, without moving the geometry it's measuring. Different from what's already there: **DimensionAuto** only auto-*places* new dimension strings, and **OverriddenDimensions** only *finds* dimensions that already have a manual override — neither lets you actually set one. This would need Revit's `Dimension.ValueOverride`/segment override API; worth checking whether it should be single-element (like the native double-click override, just faster to reach) or batch (find every dimension currently reading X and override it to Y in one pass).

