# Converter and KDA base mod

Converter V2.0 / 2026-10-26-kda12 reads Automation BeamNG ZIP exports and produces car-only packages. Native BeamNG car mods and standalone .car files are unsupported. The separately installed runtime is V1.0 / 2026-10-28-kda25.

## Convert and install

Install the base GameData/KDAVehicles first, with the dependencies linked in README. The optional garage is GameData/KDAGarageBuilding. In the converter select the source ZIP, the whole KSP folder (the folder containing KSP_x64.exe), an output folder and an optional existing save. Body paint can use the exported primary color or an explicit override. Generated packages contain one vehicle folder, craft and validation report, plus source notices.

The five-part chassis is uncrewed. Add a physical KDA race seat and doorway marker, position them and assign a Kerbal in the editor. Planned seats records intent; it does not create hidden crew slots.

The installer checks a matching ABI, sufficient framework build, exact DLL/stamp hash, required crew files, duplicate runtime/part IDs, paths and process state. It copies only the converted car and craft, preserves scoped backups and rolls back failures. It never upgrades, downgrades or replaces the runtime, crew or garage. Install a current base GameData separately when those files need updating.

## Current import support

Supported exports retain torque/boost curves, gearing, drivetrain selection, active center/electronic axle capabilities, source-weighted CG, axle load distribution, tire dimensions and grip. Hardware detection follows selected export controllers; AWD alone does not invent electronic vectoring. Supported fixtures supply lights, rear brake surfaces, steering-wheel rigs, dashboard definitions and exhaust outlets. Articulation and material conversion have documented limits; see Vehicle Lighting.md, Grille Materials.md and KDA Long Travel.md.

The base runtime adds garage tuning, Standard/Rally/Desert suspension, driving modes and AWD profiles, telemetry, lighting/camera controls, weightless attached seats/crew, rechargeable nitrous, independent tire grip and exhaust tunes. These features are shared runtime code, not copied into each converted vehicle.

## Build and checks

The desktop and Python CLI share service.convert and installer.install. Build instructions are in source/README.md. The executable includes conversion libraries and original KDA effect samples, with their retained notices; it contains no GameData framework or garage payload. Source tests cover parsers, materials and scoped installation, with external export packages needed for source integration cases. Current checks and limits are recorded in release-manifest.json. No new in-game driving certification is implied.

This development branch uses original shared hybrid engine audio instead of exported engine recordings. See [Hybrid audio](Hybrid%20Audio.md). The current base runtime is kda24; converter V2.0 remains kda12.
