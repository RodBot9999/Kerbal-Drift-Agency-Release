# KDA Vehicle Converter V2.0

Build **2026-10-26-kda12** produces car-only packages. Install KDA's base `GameData/KDAVehicles` once before using **Convert and install**. The optional `GameData/KDAGarageBuilding` is installed separately and is never changed by the converter.

## Offline conversion

Run `Converter/KDAVehicleConverter.exe` from the standalone release. Choose an Automation BeamNG ZIP export, output directory, texture quality and optional KSP/save destination. Convert only creates a validated package; Convert and install also installs it with backups. A standalone .car lacks the visual geometry and is rejected. Python, NumPy, Pillow and the GPL MU writer are bundled; no AI, tokens, internet or Blender is required.

Disable Merge All Fixtures for separate details; merged exports also work. Texture caps are High 2048, Balanced 1024 and Light 512. Mesh triangles are not automatically simplified.

## Independent wheels

kdaconvert/wheel_config.py writes KSP nodes directly from imported corner radius, grip calibration and drive shares, plus the explicit KDA wheel contract. No template substitution is used. Existing part IDs, module order, rig transforms, selection, mass, springs/dampers/anti-roll, toe, brake torque and steering settings are preserved.

The unused legacy steering curve is omitted because stock automatic steering adjustment is disabled. A neutral two-key motor curve preserves KSP's 2 kNm setup normalization; KDAWheelMotor.OnDriveUpdate sets actual engine/gearbox/differential torque after native curve evaluation. Engine behavior, SC modes, TCS/ABS and the 3x grip slider are unchanged. vehicle.json records the generator version.

## Framework and crew

Packages contain the body, four wheel parts, meshes/materials, hybrid audio metadata, signed aero data and an assembled craft. They do not contain the KDA runtime, crew addon or garage. RWD/FWD/AWD weights, turbo pressure/torque, ratios and tire dimensions come from the selected export. Imported gearbox/induction types are locked. Supported independent suspension animates cosmetically; unsupported layouts remain rigid art. Chassis/exhaust/engine geometry is retained.

The base KDA mod provides race seats and doorway support. The generated five-part craft is an uncrewed chassis: add physical seats/markers in SPH and assign Kerbals. Planned seats is metadata, not hidden slots. Install the repository base GameData once, along with the separately distributed required dependencies.

Installer checks reject running KSP, missing/old/modified base frameworks, duplicate runtimes, invalid paths and conflicting part IDs. It copies only the new car folder and craft, with scoped backups and rollback. Update KDA's base mod separately; the converter never updates shared framework or garage files.

## Build and verification

From the repository source directory, use Python 3.12, converter/requirements.txt and PyInstaller. Run tools/build_converter.ps1 -Python <python.exe>. Runtime compilation requires a user-owned KSP installation; game assemblies are not redistributed.

Set PYTHONPATH=converter and run python -m unittest discover -s converter -p test_wheel_config.py -v. For installation tests set KDA_TEST_PACKAGE to a fresh package and run the same discovery command with test_desktop_install.py.

Regression fixtures contain functional values for 16 previously tested wheels across MITATA, Corvette, Leopard and Lizard, without CFG templates or legacy curves. Other tests may require external ZIPs/fixtures. Native KSP verification is separate from file validation.

Original converter software is GPL-3.0-only, copyright (C) 2026 Roderic Systems; see COPYING and ../../LICENSE.md. The MU writer retains GPL-2.0-or-later; the combined converter is distributed under GPL version 3. Preserve corresponding source and upstream notices. Export source asset licenses remain in force. Code and asset license scopes are documented separately in the repository license map and asset terms. Material translation, tire calibration and engine braking approximations are documented in the repository manuals.

This development branch uses original shared hybrid engine audio instead of exported engine recordings. See [Hybrid audio](../../documentation/Hybrid%20Audio.md). Main and installed stable builds are unchanged.
