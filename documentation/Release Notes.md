# Kerbal Drift Agency — kda25 release

October 28, 2026. Runtime V1.0 / 2026-10-28-kda25;
Converter V2.0 / 2026-10-26-kda12. Build identifiers are independent.

## Garage update — October 29, 2026

- One KDA badge in the mod toolbar opens the garage menu. A placed KK garage
  opens its workshop; without a placed building, the SPH/VAB overlay is available.
- Entering from an editor keeps the current car. Return to SPH/VAB restores the
  stock editor with that car; Exit Garage returns to the Space Center.
- Brighter motorsport workshop, darker matte checkerboard tiles, LED lighting,
  cabinets and a Kerbal Drift Agency neon sign.
- Hovering over the world garage displays a building tooltip.
- Right-drag camera orbit and scroll zoom work inside the garage's car viewport.
- Build supports native WASDQE part rotation and the stock fine-rotation modifier;
  rotation stays blocked while typing or using a dialog.
- Garage saves synchronize the stock editor's saved name and undo checkpoint,
  preventing false unsaved/overwrite warnings for the unchanged current craft.
- Cars / Prebuild Car supplies complete Lizard GT and finished Rabbit Rally x
  templates, including saved seat positions and outward door markers. These
  templates are inside GameData, so a clean installation needs no saved crafts.
- Other four-wheel imports can be prebuilt from attachment nodes with an inferred
  or package-specified doorway. Check marker placement and add a seat. Existing
  saved cars are offered for loading instead of generating another copy.

## Included cars

- **Lizard GT** — the original included rear-wheel-drive V8 car.
- **Rabbit Rally x (finished)** — the finished AWD rally setup, supplied as
  `Ships/SPH/Rabbit Rally x KDA(finished).craft`, with its matching vehicle
  assets, physical seat and doorway. Copy the supplied craft into your save's
  `Ships/SPH` folder and assign crew before launching.

## Driving and tuning

- Road, Sport, Race and Drift stability profiles, with adaptive steering,
  cornering allowance, traction aids and ABS.
- Export-dependent active AWD centre redistribution and electronic axle
  vectoring, with AWD-specific road/sport/race/drift behavior and temporary
  rear-drive decoupling while holding the handbrake.
- Accepted Extreme Series high-speed Race/Sport oscillation correction.
- Standard, Rally and Desert suspension hardware, ride height, travel, track
  width, springs, damping, anti-roll tuning, graphs and visual articulation.
- Separate tire-grip and brake-strength tuning, editor fuel-use multiplier,
  stationary upside-down recovery and stationary-warp protection.

## Garage and effects

- Cars / Build / Tune garage, showroom views, saved-car selection/deletion,
  native build-tool access, live suspension previews and power graphs.
- Optional world garage with the default the default KSC location location near KSC.
  Install Kerbal Konstructs and Custom Pre Launch Checks separately. Its
  position is optional: move it using Ctrl+K, or omit Garage-instances.cfg.
- Original hybrid engine audio with deeper engine-family character. Player
  controls are Engine volume and Exhaust in the garage; volume is also
  adjustable in flight. Exported engine recordings are not bundled.
- Grounded tire smoke, configurable afterfire and rechargeable nitrous,
  imported lights, steering-wheel animation and miniature dashboard display.
- Optional independent rover ECU with Unstable and Normal add-on profiles.
- kda25 corrects masked exterior depth/shadows while preserving PBR paint, glass and grille openings.

## Installation

1. Install the external dependencies from the README download links.
2. With KSP closed, copy `GameData/KDAVehicles` into KSP's `GameData` folder.
3. Optionally copy `KDAGarageBuilding` and install its external KK dependencies.
4. Use Cars / Prebuild Car, or copy the supplied craft files into your save's `Ships/SPH` folder.
5. Run the v2 converter for additional Automation BeamNG ZIP exports. Select
   the whole KSP installation directory, not its GameData folder. The converter
   installs cars only; install the base mod separately first.

Keep one runtime copy. Back up existing saves and mod files before upgrading.
No third-party KSP mods are embedded. The converter is offline and requires
neither AI nor an internet connection for conversion.

## Compatibility and licenses

Designed for KSP 1.12.5. Native BeamNG vehicle mods are unsupported.

Runtime code is MIT; converter software is GPL-3.0-only. Original KDA audio and
manual text are CC BY 4.0. Branding permission is separate. Vehicle assets keep
their supplied Automation export notices and applicable third-party terms.
See LICENSE.md, ASSET-LICENSES.md and the vehicle SOURCE-LICENSE.txt files.
Public binary distribution must include access to matching converter source.
