# Exterior rendering compatibility

KDA supplies masked depth and shadows for affected Resurfaced cutout materials.
Paint retains its PBR appearance; glass remains transparent and genuine grille
holes remain open. Stock forward rendering keeps its native material queue.
Active deferred rendering uses the appropriate cutout G-buffer queue.

The helper shares existing geometry and opacity masks, adds no visible surface,
and changes no collider, suspension, engine, audio or driving behavior. It needs
no car reconversion. It works automatically when affected materials are present.

Stock materials without Resurfaced remain unchanged. External rendering mods
remain separate downloads; see the README dependency table.
