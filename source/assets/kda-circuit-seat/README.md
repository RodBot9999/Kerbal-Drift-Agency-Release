# KDA Circuit racing seat

Original procedural artwork by Roderic Systems, authored for KDA. The generator
uses analytic shell surfaces and beveled solid cushions; no downloaded mesh,
texture, UV layout, logo or surface tracing is an input.

The generic bucket-seat design has a tapered shoulder arch, two real shoulder
harness openings, divided thigh/back pads, teal seams and a KDA headrest badge.
The fabric pattern and badge bitmap are generated locally. No font file is
bundled with the asset.

## Drop-in fit

The legacy seat's installation envelope is retained as six numerical limits,
in metres with X right, Y up and Z forward:

- Minimum: `(-0.228582576, -0.023392351, -0.244214550)`.
- Maximum: `(0.228852719, 0.716792881, 0.332983434)`.
- Size: **0.457435295 x 0.740185233 x 0.577197984 m** (width, height, depth).
- Part mounting origin remains `(0,0,0)` with no root scale/rotation changes.
- Existing crew origin remains `(0,0.045,0.035)` in the external frame.

The existing part name, model URLs, selection collider, airlock trigger, IVA
seat transform, portrait light and camera/crew placement are preserved. Saved
craft need no seat repositioning for this asset replacement. The new cushion
contours are independently designed; the envelope does not imply identical
contact surfaces to the preceding model.

## Files and rebuilding

`kda-circuit-seat.obj` and `.mtl` are editable exports of the original geometry.
`geometry.json` records dimensions and provenance. Textures are `seat_fabric.png`,
`seat_solid.png` and `kda_seat_badge.png`.

From the repository root, run:

```powershell
python source/tools/build_crew_models.py --output source/.build/assets/Crew/Models
python source/tools/validate_crew_assets.py --models source/.build/assets/Crew/Models
```

Copy `seat.cfg` and `imported-crew.cfg` from `GameData/KDAVehicles/Crew` to the
parent `Crew` staging directory before running the validator. Build dependencies
are NumPy and Pillow; Pillow must be able to locate `DejaVuSans-Bold.ttf` for the
badge. The existing vendored MU writer writes the KSP assets. No model service,
mesh simplification library or UV unwrapping service is required.

The seat uses 2,596 triangles and five material batches per external/IVA model.
Fabric is 512 px; the lettering has its own small texture. Opaque stock
`KSP/Diffuse` materials require no extra shader mod. The doorway marker is
unchanged. Generator code follows the project's GPL-3.0-only build-tool license;
the generated artwork is **CC BY 4.0**, credited to Roderic Systems — Kerbal Drift Agency. See [LICENSE.md](LICENSE.md).
