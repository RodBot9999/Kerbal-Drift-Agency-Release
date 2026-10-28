# KDA license map

Copyright (c) 2026 Roderic Systems for independently authored KDA code.

This is a distribution containing separately licensed components. The following
licenses apply only to code for which Roderic Systems can grant these rights.
Upstream notices and asset licenses take precedence for their own components.

| Component | License and scope |
| --- | --- |
| KDA runtime | **MIT**: original C# code in `source/plugin/**/*.cs`, the corresponding `KDAVehicles.dll` and `KDACrew.dll`, and `source/tools/build_plugin.ps1` / `build_crew_plugin.ps1`. Full text: [MIT](LICENSES/MIT.txt). |
| Optional rover ECU | **MIT**: original C# in `source/ecu/**/*.cs`, `KDAECU.dll`, and `source/ecu/build.ps1` / `test.ps1`. The original MU-generation script `source/ecu/build_model.py` is **GPL-3.0-only**, using the separately licensed MU writer. Model/texture artwork follows the separate asset scope below. |
| KDA converter | **GPL-3.0-only**: original software in `source/converter/**/*.py`, `source/converter/convert.ps1`, and the combined `Converter/KDAVehicleConverter.exe`. Full text: [GPL version 3](LICENSES/GPL-3.0-only.txt). |
| Other KDA build tools | **GPL-3.0-only**: original software under `source/tools` and the garage generation/preview Python tools under `source/assets/kda-garage`, except the two MIT runtime build scripts named above and separately licensed vendor files. |
| Vendored MU writer | Original **GPL-2.0-or-later** grant and notices retained. The combined converter is distributed under GPL version 3. See `source/tools/vendor/io_object_mu` and `LICENSES/io_object_mu-GPL-2.txt`. |
| Other bundled conversion libraries | Their respective upstream licenses; retained under `LICENSES/Converter-runtime`. These are not relicensed as KDA code. |
| Vehicle/seat models, textures, audio, Sparky branding and other artwork | **Excluded from the software grants above.** Use the component's actual terms and provenance in [CREDITS.md](CREDITS.md). Original KDA audio and manual text are CC BY 4.0 under [ASSET-LICENSES.md](ASSET-LICENSES.md); branding has a separate limited permission there. Other artwork retains its own terms. |

Converted user assets do not become GPL merely because the converter writes them.
Their original licenses and any rights in user contributions continue to apply.
Third-party assets are not relicensed by these software grants; retain their
original terms and notices. Exported engine recordings are not included.

## Corresponding source

Converter source, vendored MU writer, requirements and build instructions are in
`source/`, with instructions in [source/README.md](source/README.md). For a binary
release, provide the matching source tag/archive and applicable notices next to
the download. Current development repository:
https://github.com/RodBot9999/Kerbal-Drift-Agency-Release

Public converter binaries must be accompanied by access to their matching
source and applicable notices.
