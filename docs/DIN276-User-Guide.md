# DIN 276 Cost Estimator — User Guide

The DIN 276 Cost Estimator classifies model elements into DIN 276
Kostengruppen — 2nd-level (3-digit) groups, and for walls/floors/roofs a
real 3rd-level breakdown (e.g. 331 Tragende Außenwände vs. 332 Nichttragende
Außenwände) — computes their real quantities, and multiplies by unit rates
you enter to produce a live cost estimate. It ships in Ottawa Tools → the
panel containing "DIN 276 Cost Estimator".

## What it does — and does not — do

- It does **not** ship with any built-in €/unit rates. Construction costs
  vary too much by region and year to hardcode responsibly, so every rate
  table starts at zero. Type rates in directly, or import a rate sheet
  you've exported before (see **Import / export**, below).
- It only auto-classifies elements into Kostengruppen that have an
  unambiguous default mapping (see **How classification works**). Elements
  with no mapping — and no explicit override — simply don't appear in the
  table; they are not guessed at. An explicit `Kostengruppe` override is
  never restricted to the codes this tool ships a name for — any code you
  type in (a 3rd-level MEP sub-code, or a firm-specific one) is still
  classified, priced, and reported, just under a generic "Kostengruppe
  &lt;code&gt;" label instead of an official DIN 276 name.
- "Assign to elements" (see below) is the only action that writes anything
  to the model. Everything else — scope selection, quantity takeoff, rate
  entry, import, export — only reads the model or a file on disk.

## How classification works

Each element is matched to a Kostengruppe by a two-tier lookup:

1. **Explicit override — checked first.** If the element has its own
   `Kostengruppe` parameter (a project or shared parameter your firm adds —
   see **Which parameter it uses**, below) with a value, that value wins,
   whatever the built-in rule table below would otherwise say. This lets you
   correct or extend the automatic classification per-project without
   touching the tool itself.
2. **Built-in rule table — used if there's no override.** A small set of
   categories map to a Kostengruppe automatically:

   | Category | Kostengruppe |
   |---|---|
   | Walls, load-bearing (Revit's own Structural Usage = Bearing/Shear/Combined), exterior | 331 Tragende Außenwände |
   | Walls, non-load-bearing (Structural Usage = Non-bearing, or not marked Structural at all), exterior | 332 Nichttragende Außenwände |
   | Walls, load-bearing, interior | 341 Tragende Innenwände |
   | Walls, non-load-bearing, interior | 342 Nichttragende Innenwände |
   | Doors / Windows, hosted in an exterior wall | 334 Außentüren und -fenster |
   | Doors / Windows, hosted in an interior wall | 344 Innentüren und -fenster |
   | Floors | 350 Decken |
   | Roofs | 360 Dächer |
   | Plumbing fixtures, pipes | 410 Abwasser-, Wasser-, Gasanlagen |
   | Mechanical equipment | 420 Wärmeversorgungsanlagen |
   | Ducts | 430 Raumlufttechnische Anlagen |
   | Electrical equipment, electrical fixtures, lighting fixtures | 440 Elektrische Anlagen |
   | Communication devices, fire alarm devices, security devices | 450 Kommunikations- und Sicherheitstechnik |

   The wall tragend/nichttragend split reads Revit's real **Structural
   Usage** setting (Properties palette, when a wall's own **Structural**
   checkbox is on) rather than guessing — a wall nobody marked Structural in
   Revit is treated as nichttragend, which is the correct DIN 276 answer
   either way. Doors and windows get their own 334/344 code directly
   (a door is never itself "tragend"), following whichever exterior/interior
   split their host wall resolved to.

   Categories not listed here (structural columns/framing, and most
   nutzungsspezifische equipment) have no unambiguous DIN 276 home and are
   deliberately left unmapped — give those elements an explicit
   `Kostengruppe` value if you want them included.

   The full built-in code table also includes the rest of the well-known
   3rd-level breakdown under 330/340/350/360 (333 Außenstützen, 335–339
   Außenwandbekleidungen/Sonnenschutz/sonstiges, 343/345/346/349 for
   interior walls, 351–359 for Decken, 361–369 for Dächer) plus the
   remaining 2nd-level groups (370, 390, 470, 480, 490) — none of these have
   an automatic category rule yet, but every one of them is a valid explicit
   `Kostengruppe` override value with its correct official name and unit.

Quantities are read from Revit's own area/volume/length parameters and
converted out of Revit's internal feet-based units into real m²/m³/m
regardless of your project's display unit settings, so the numbers in the
report are always genuinely metric.

## Which parameter it targets

The tool primarily reads and writes a single parameter, by name:
**`Kostengruppe`** (Text).

Unlike an ordinary project parameter, you don't have to add this one
yourself. The first time you click **Assign to elements**, if the project
doesn't already have a `Kostengruppe` parameter, the tool creates one and
binds it to every relevant model category (Walls, Floors, Ceilings, Roofs,
Doors, Windows, Structural Columns, Structural Framing, and the MEP
categories the built-in rule table covers) — so no element gets skipped
just because nobody set the parameter up in Manage → Project Parameters
first. It's created through a small Ottawa Tools-owned shared parameter file
kept at a fixed location (`%AppData%\Ottawa Tools\Din276SharedParameters.txt`),
so re-running this on the same project, or on a different one, reuses the
exact same parameter definition rather than creating a lookalike duplicate
each time — the parameter still appears as a normal project parameter under
Manage → Project Parameters, indistinguishable from one your firm added by
hand. If the project already has a `Kostengruppe` parameter (Text or
Number, either storage type), the tool uses that one as-is and doesn't
touch its binding.

**Fallback, only if that still isn't possible for a given element:** if
`Kostengruppe` genuinely can't be written to a specific element (its
category wasn't included in the bind, for instance), the assignment falls
back to the built-in **Assembly Code** field (`Baugruppenkennzeichen` in
German Revit) and, failing that, **Type Comments** — whichever is writable
first. Both of those fields are commonly already used on a project for
UniFormat/OmniClass classification or other notes, so this fallback can
overwrite existing data on that specific element; it's a deliberate
last-resort tradeoff so "Assign to elements" doesn't silently skip
elements, not the normal path. In the ordinary case — parameter bound
successfully — every classified element writes to its own dedicated
`Kostengruppe` parameter and nothing else is touched.

Before you ever click **Assign to elements**, the on-screen report
(classification, quantities, pricing) works exactly the same whether or not
`Kostengruppe` exists yet — only the write-back needs it, and it creates
it automatically at that point.

## Step-by-step usage

1. Open a project, then launch **DIN 276 Cost Estimator** from Ottawa Tools.
2. Choose a **scope**: *Whole project* or *Active view*. The table
   recalculates immediately.
3. Review the **Kostengruppen** table. Each row shows the matched
   Kostengruppe, its quantity (already in m²/m³/m as appropriate), an
   editable **€/Einheit** rate box, and the row's subtotal. Type a rate to
   see the subtotal and the **Gesamtsumme** (grand total) update live.
4. **Import rates**, optionally: load an `.xlsx` rate sheet with `KG` and
   `€/Einheit` columns (the same format the tool exports) to fill in rates
   in bulk instead of typing each one.
5. **Export report**, optionally: save the current table (including your
   rates and the grand total) as an `.xlsx` file.
6. **Assign to elements**, optionally: writes each currently-classified
   element's resolved Kostengruppe code onto its own `Kostengruppe`
   parameter, creating and binding that parameter first if the project
   doesn't already have it (see **Which parameter it targets**, above). You'll
   be asked to confirm first. The summary dialog afterward reports how many
   elements were assigned versus skipped (skips are rare — the fallback
   chain described above covers most cases where the primary parameter
   isn't writable for some element). This is the one step that changes the
   model, and it runs inside a single transaction: if anything goes wrong
   partway through, the whole assignment — including the parameter
   creation — is rolled back rather than left half-applied.
7. **Close** when you're done — Import/Export/rate entry never require
   closing the dialog to take effect.

## Notes for firm-wide use

- Because classification and quantity takeoff are non-destructive, it's
  safe to open the tool and explore scope/rates on any project without
  risk — nothing is written until you explicitly click **Assign to
  elements** and confirm.
- To standardize rates across projects, export a report once rates are
  filled in, save that `.xlsx` as your firm's DIN 276 rate sheet, and
  import it at the start of future projects.
- To override the automatic classification for elements the built-in rule
  table doesn't cover (or gets wrong for a specific project), add a
  `Kostengruppe` project parameter if the project doesn't already have one,
  then set it directly on those elements — the override always wins over
  the built-in rules.
