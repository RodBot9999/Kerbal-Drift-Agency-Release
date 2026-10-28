# Optimized seat source

`master.obj` is an unchanged copy of the user-supplied
`baquet_Recaro_Pole_Position.obj` (25,412 triangulated faces).
Its SHA-256 is `728FE04126459355CD6306DAC5C95CC4C5D76E3C39F192DCA0C29421531A1B4A`.
The original in Downloads is not edited.

`optimized-ksp.obj`, its MTL and `seat_fabric.png` are editable derived assets.
They use meters, Y up and +Z forward. Triangle count: 4,880, including added
back surfaces and boundary walls. Width is approximately 0.457 m and height
0.740 m. Normal seams and packed UV charts are intentionally split vertices.

Run `tools/build_crew_models.py` with the workspace Python runtime. Offline
dependencies are fast-simplification 0.2.0 and xatlas 0.0.11, located in
`.build/seat-deps`. The MU writer is already vendored in `tools/vendor`.
The builder retains the part attachment pivot, crew slot and hatch; it writes
matching external and IVA geometry. The occupant pivot is 9 cm lower in the
Recaro bowl (local position 0, 0.045, 0.035 m), with the driver camera following.
`--procedural-seat` builds the previous
block seat instead. The installed previous version is also retained under
`archive/crew-before-recaro-20261014`.

Simplification preserves border vertices before adding thickness. The
upholstery and rear shell receive inward backing surfaces and boundary walls,
not caps across the harness holes. The rear shell is spaced 9 mm outward to
prevent intersections between its simplified tessellation and the upholstery. Material separation is fabric, shell/trim
and metal. Materials use stock KSP/Diffuse without a new shader dependency.

Rebuilding this asset does not update the converter or vehicle controller.
Original model authorship/license is not established by the OBJ file; retain
the original download's attribution/license before distributing derived art.
