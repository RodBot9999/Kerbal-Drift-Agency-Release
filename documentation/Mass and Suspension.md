# Mass and suspension — V1.0 / 2026-10-16-kda5

## Changes

The converter reconstructs the selected export's mass moments from explicit JBeam nodes and symmetric generated pressure-wheel rings. It uses the selected PC parts and slot defaults, inherited nodeWeight, row overrides and scalenodeWeight. It safely evaluates numeric expressions without running Lua. The exported total mass remains authoritative; reconstructed mass moments determine the CG and axle distribution. Wheel ring mass is placed at the selected axle midpoint, checked against visual wheel positions.

Coordinates use the same BeamNG-to-KSP axes and hub-height origin as the mesh. The body CG is solved after accounting for the four native 11 kg wheel parts and 50 kg full native fuel, so those masses are not counted twice. Fuel consumption and ordinary payload still affect native mass. KDAImportedMass applies the calibrated body offset to older craft too; KSP's flight integrator applies it to the rigidbody. Existing wheels retain their native mass and inertia; no source inertia tensor is imported.

Unsupported transforms, incomplete/unresolved weights, unknown generator overrides, implausible CG or reconstructed mass more than 8% from metadata produce a labelled generic CG fallback. These are reference-pose estimates, not measurements from a running BeamNG car; camera/fuel preprocessing and deformation are not simulated.

## Export checks

| Export | Front weight estimate | CG above reference tire plane | Reconstructed vs metadata mass |
| --- | ---: | ---: | ---: |
| rabbit_rally_x | 63.8% | 0.299 m | 908.9 / 913 kg |
| lizard_gt | 60.5% | 0.253 m | 1052.9 / 1033 kg |
| lizard_gtr | 61.7% | 0.286 m | 1254.0 / 1232 kg |
| el_corvetttellako | 55.1% | 0.223 m | 1153.6 / 1144 kg |
| leopard_safire_ksp | 57.7% | 0.222 m | 868.2 / 862 kg |
| mitata_mk2_na | 54.4% | 0.236 m | 1133.1 / 1118 kg |

## Seats and crew

Attached KDA race seats and doorway markers are physicsless and massless in the editor and flight. Scoped Harmony postfixes remove the seat's native crew/inventory mass contribution after Part mass updates. The crew member and items remain present, portraits/command/boarding continue to use native KSP, and an exited EVA Kerbal retains normal stock mass. Global crew mass is unchanged. Standalone root seats are excluded from the runtime normalization: this feature targets seats attached to cars. Both ModuleManager and HarmonyKSP must be installed separately.

## Anti-roll bars

Anti-roll rate remains adjustable in N/mm. An axle owner compares physical spring compression with the previous imposed bar displacement removed. It commands opposite spring-reference preload displacements through native wheel colliders, so actual native spring loads and tire contact loads respond together. Each displacement is bounded by 25% of the available 160 mm travel, using the softer spring's force authority. Both-airborne axles reset; a single contact uses the remaining native spring contact. Native contacts cannot pull on the ground, so requested elastic preload need not equal measured tire force at droop or bump stops. No upright torque or extra tire downforce is added.

Support-mass calibration now updates with actual vehicle mass and CG instead of remaining cached at launch. Existing presets, SC/AWD laws, engine braking, grip calibration and aerodynamic laws remain unchanged. Grounded tire smoke is implemented in the effects runtime.

## Telemetry schema 9

Existing columns stay in place. wheels.csv appends bar_preload_m, physical_compression_m and supported_mass_t. Anti-roll request/cap/capped/applied_observed now describe elastic preload requests in native kN, not separately applied body forces. Use load for actual native tire force and antiroll_gate for both-airborne/one-contact/spring-coupled state. setup.cfg includes KDAImportedMass fields; parts.csv records mass and crew. Full airborne observations have no invented ground plane.

## Verification


## Sources

- [BeamNG nodes: units, coordinates and nodeWeight](https://documentation.beamng.com/modding/vehicle/sections/nodes/)
- [BeamNG pressure wheels: rays, node masses and whole-wheel weight overrides](https://docs.beamng.com/modding/vehicle/sections/wheels/)
