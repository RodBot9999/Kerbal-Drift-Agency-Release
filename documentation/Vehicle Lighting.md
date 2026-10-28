# Vehicle lighting — V1.0 / 2026-10-17-kda6

## Controls

- The normal KSP **Lights** group (default **U**) switches headlights on/off. The driving HUD also has a Lights button.
- Select low/high beams in the HUD or the body's part menu.
- Left, right and hazard buttons work where the export supplies those lamps. They can also be bound to custom action groups.
- Brake lamps follow the normal driver brake group independently of the headlights. Automatic stability corrections and engine braking do not flash them.
- Reverse lamps follow reverse selection when the export defines reverse lamps.
- Light brightness is adjustable in the editor and flight. Editor preview is available in the body menu.

## What comes from Automation

The converter reads the selected JBeam parts' SPOTLIGHT/POINTLIGHT props, their reference nodes, colors, ranges and base rotations. It reads glowMap simpleFunction definitions and the included on-material emission colors. It does not execute BeamNG Lua. Missing dynamic dashboard materials are reported and skipped.

Rabbit supplies 33 source props and 11 usable glow groups; Lizard supplies 24 props and six groups. Neither current export defines a reverse-lamp function. Some source rear fixtures are authored as white low/high-beam lamps: KDA preserves their exported functions and colors rather than guessing replacements.

Material emission masks preserve lens holes and body cutouts. Resurfaced materials receive per-renderer emission properties; the stock shader fallback uses a separate additive pass without colliders. Paint and exterior material shaders stay intact.

Up to eight projected lights and two shadow lights are active per car. Nearby duplicate low/high props share a light object. Forward road beams receive priority; all supported fixture surfaces can still glow. Packed vessels, empty body batteries and cars beyond 450 m stop rendering lights. The current implementation checks available ElectricCharge but does not drain it.

## Files and extension points

- `source/converter/kdaconvert/lighting.py`: source props, glow mapping and coordinate conversion.
- `source/plugin/LightingCore.cs`: light-function evaluation.
- `source/plugin/KDALighting.cs`: projected beams, emission and controls.
- Each converted vehicle gets `lighting.cfg`, emission PNGs and lighting metadata in `vehicle.json`.
- `Crew/vehicle-lighting.cfg` adds the module to existing KDA body prefabs without replacing their saved tuning. A current car also needs its own lighting data and masks.

Source range/color data is retained; brightness and beam shape are Unity approximations. Static reference-node frames are supported; arbitrary scripted or animated BeamNG props are not reproduced. Exports without light data do not receive invented lamps.

Source format references: [BeamNG props](https://documentation.beamng.com/modding/vehicle/sections/props/) and [glow maps](https://documentation.beamng.com/modding/vehicle/sections/glowmaps/).
