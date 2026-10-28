# Source and rebuilding

The release has one installable asset tree: the repository's top-level `GameData`. Runtime source is under `source/plugin`; converter source is under `source/converter`. Build output stays in ignored `.build` directories, so generated DLLs and development assets do not become duplicate release copies.

## Runtime and crew

From this `source` directory in Windows PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_plugin.ps1 -KspRoot "C:/path/to/Kerbal Space Program"
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_crew_plugin.ps1 -KspRoot "C:/path/to/Kerbal Space Program"
```

Outputs are written under `source/.build/runtime/GameData/KDAVehicles`. Copy verified DLLs and the matching framework.json into the release GameData explicitly. `-OutputDirectory` can select a different build-only GameData root. The runtime build reads the current release build/ABI from `GameData/KDAVehicles/framework.json`; it does not derive its version from the converter.

KSP/Unity assemblies come from a legally installed KSP copy. KDACrew also needs the separately installed HarmonyKSP DLL. Game assemblies and dependency mods are not bundled. Keep one runtime and one crew DLL in the player installation.

## Converter

The converter is V2.0 / 2026-10-26-kda12. It creates and installs car-only packages; install the KDA base GameData separately. It never bundles or replaces the runtime, crew addon or garage. `kdaconvert.service.convert` and `kdaconvert.installer.install` are shared by the GUI and command line.

```powershell
python -m venv .build/venv
.build/venv/Scripts/python.exe -m pip install -r converter/requirements.txt PyInstaller==6.22.3
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_converter.ps1 -Python .build/venv/Scripts/python.exe -OutputDirectory ../Converter
```

Use Python 3.12. The build includes NumPy, Pillow, the independent wheel generator and vendored MU writer. Blender is not required. Applicable library notices remain in LICENSES and CREDITS.md.

Set `PYTHONPATH=converter` for source CLI and tests. `python -m kdaconvert --help` documents conversion, inspection and installation options. Run `python tools/test_converter.py`; the runner sets current source imports and an ignored writable temp directory. Source-export integration tests are skipped unless KDA_TEST_EXPORTS points to extracted analysis-corvette/analysis-leopard folders, or a package is supplied through KDA_TEST_PACKAGE. Parser/material/installer unit tests use generated fixtures. Do not mistake external-test prerequisites for release assets.

## Models and validation

`assets/kda-garage/build_garage.py` builds the current original garage. It accepts `--output` for a staging folder. `assets/kda-garage/preview_garage.py` accepts `--asset` and `--output`. The installable model is `GameData/KDAGarageBuilding/Garage/garage.mu`, catalog ID `KDAGarage`.

`tools/build_crew_models.py` stages seat/internal/doorway meshes under `.build/assets`; `--output` selects another destination. Its reports are under `.build/asset-reports`. Seat rebuilds need NumPy/Pillow and the optional fast-simplification/xatlas tools described by the retained seat source. Current assets are already supplied; rebuilding is optional. `python tools/validate_crew_assets.py` validates the release crew assets and writes `.build/validation/crew-assets.json`.

## Documentation and release checks

`tools/build_kda_manuals.py` contains the technical manuals. `documentation/ai-guides-content.json` is the editable source for Prompts and AI Context. The builder creates five PDFs, their AI guide Markdown and manual-content.json. Use `--only` to select manuals. ReportLab builds PDFs; pypdf/pdfplumber and Poppler support review. Render and inspect changed pages.


Original runtime C# code and its two build scripts use MIT; original converter software and the other KDA build tools use GPL-3.0-only. See ../LICENSE.md for exact scope and exclusions. Keep upstream notices intact; the MU writer retains GPL-2.0-or-later and is combined under GPL version 3. Full texts are under ../LICENSES, with copies beside runtime/converter source and binaries. These code licenses do not grant rights to models, textures, audio or branding. Seat/export/audio provenance remains recorded in ../CREDITS.md.
