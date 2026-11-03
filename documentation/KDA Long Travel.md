# Rally and Desert suspension

## Start here

1. Open a car in **KDA Garage → Tune → Travel / Kit**.
2. Select **Front axle → Rally 300**, then **Rear axle → Rally 300**.
3. Use **Ride**, **Full bump**, **Full droop** and **Articulate** to inspect the tires and suspension. The spring calculator includes a 75% rough-ground load reserve; the estimated parked sag is shown separately.
4. Read the clearance message. It samples tire-shaped boxes against the installed body colliders through travel and steering lock; it is approximate and cannot guarantee that every painted panel or fixture clears the tire.
5. **Apply to car**, then **Save Car**. Existing craft files require no reimport.

Start with small bumps and jumps. The kit adds suspension movement; it does not make every imported body suitable for severe off-road use. Body collisions still exist. A narrow road-car shell may need wider track or different height/travel, and a large truck should have suitable body geometry.

## Hardware profiles

| Profile | Wheel travel | Static height offset | Calculator sag ceiling | Track extension per side |
|---|---:|---:|---:|---:|
| Standard | 160 mm fixed | Current chassis preset | Original rate-dependent value | None |
| Rally | 300 mm | +80 mm | 35% | 0 mm |
| Desert | 550 mm | +180 mm | 40% | +150 mm |

Rally/Desert travel can be adjusted from **200–700 mm** independently per axle. These are initial kit settings, not a claim that the source car has this hardware. Standard remains the default for existing and newly imported cars.

The buttons create drafts. They change suspension hardware/settings only: driving mode, engine, LSD, tire grip and aero calibration are retained. Applying a Road/Sport/Race/Drift **chassis preset** returns the suspension to Standard 160 mm and applies that preset's suspension values. You can then select a long-travel kit again. Driving mode is separate from the chassis preset.

## Controls

- **Wheel travel:** total full-extension to full-compression movement.
- **Ride height:** static body height offset, independent of total travel.
- **Sag ceiling:** calculator input for the greatest desired parked compression before the rough-ground reserve is applied. Press **Suggest stiffer off-road spring** to update the spring draft. Changing the slider alone does not change spring rate. Sag is compression from full extension; at rest it is also the extension travel available before the wheel reaches full droop.
- **Spring rate:** effective wheel rate, 5–280 N/mm for long-travel kits. The calculator uses current editor mass, CG/axle weight distribution and Kerbin gravity, then adds a **1.75× load reserve** for rough ground. This produces recommendations 75% higher than the original parked-weight calculation, except where the rate limits apply. It is a tuning choice, not a model of every landing or terrain surface. Fuel/payload changes can change actual sag in flight.

The calculator now treats the input as a ceiling, not an exact sag promise. With no rate-limit clipping, a 20% ceiling predicts approximately **15.7% parked sag**, Rally's default 35% predicts **24.3%**, and Desert's default 40% predicts **27.1%**. The normal parked-sag calculation is unchanged and the garage shows that actual estimate. The native 10% travel preload is included in these figures. For a heavy car at the 280 N/mm cap the ceiling may not be attainable; inspect the displayed estimate.

Existing saved rates are preserved. To strengthen an existing setup, select each axle and press **Suggest stiffer off-road spring**, then **Apply to car → Save Car**. Selecting a Rally/Desert kit again also creates drafts with the new recommendations. Standard hardware and manually entered spring rates are unchanged.
- **Track extension:** per-side widening. A parallel-arm arc also changes track slightly as the wheel moves.
- **Bump stop begins:** position at which progressively increasing compression resistance starts.
- **Bump stop strength:** additional resistance approaching full bump.
- **Position damping gain:** extra damping approaching full compression, in addition to the existing slow/fast compression and rebound controls.
- **Extension limit damping:** increased rebound damping near full droop, with the native collider providing the final travel limit.

The garage rejects excessive static sag or a full-droop position outside the kit's arm reach. A body-clearance warning is advisory: inspect the car and adjust its geometry before demanding large jumps.

## Previews and graphs

Preview poses move only model pivots. The **3/4 / Left / Right / Front / Rear / Top** buttons above the Tune workspace set inspection views; right-drag orbits freely. The side views make bump and droop movement easier to inspect. The graphs keep the chosen direction and reframe for the narrower space.

Leaving Tune discards unapplied drafts and resets the travel pose. **Cars** shows the applied ride height, pitch, width and kit hardware in the normal ride pose, including after loading a saved craft. **Build** restores native model/pivot positions for placement. Closing KDA Garage keeps the applied ride pose visible in the **stock SPH/VAB**, with stock camera/placement controls restored. Native tweakable changes and craft loads refresh the presentation. Loading/deleting a car clears the previous preview bindings; leaving the editor restores native model/pivot positions. Neither the craft parts nor imported mesh vertices are permanently moved by the preview.

**Show graphs → Spring** keeps the spring-force/compression chart, including progressive bump-stop force. Estimated parked sag and remaining bump travel appear beneath it.

**Damping** shows two force charts for the selected axle: **Push up / Compression** and **Return down / Rebound**. The horizontal axis is suspension movement speed in **mm/s**, not car speed; the vertical axis is resisting force in **newtons**, for one wheel. Both charts share a force scale. Higher compression force resists a bump more strongly; higher rebound force slows the wheel returning down. Excessive rebound can prevent the tire following repeated bumps.

Use **Test wheel speed** to move the marker and read the numerical force on both charts. For Rally/Desert, **Compressed** selects the position through the travel; **At rest** sets it to the current predicted parked sag. The curves include the same corner mass, spring rate, slow/fast damping blend, position gain and droop gain used in flight. They describe the current model, not a measured damper dyno or a prediction of the next bump.

**Body roll** uses the draft rates, increased ride height, static track and load-bounded anti-roll coupling. Its approximate four-loaded-wheel model stops before bump-stop engagement or wheel unloading; it is not a jump, grip or rollover prediction.

## Physics and visuals

Standard preserves the original 160 mm reference, spring/damper behavior and native tire system. The new kits extend the existing native wheel colliders. Added bump resistance is expressed as an equivalent native wheel spring rate, so the wheel solver receives the associated supported load. KDA adds no separate downward chassis force or extra grip. The extra damping varies with stroke; extension damping opposes rebound rather than pulling the car toward the ground while airborne.

Anti-roll coupling is capped by both travel and supported corner load, rather than automatically acquiring excessive authority from longer travel. Airborne wheels do not provide imaginary ground support.

For imported cars with KDA suspension visual roots, the kits replace the old blended suspension artwork with procedural parallel wishbones, pivots, a telescoping damper and a coil. A kinematic wheel arc keeps those links consistent through travel. These are lightweight replacement kit visuals and a wheel-position approximation, **not** a reconstruction of the source car's full double-wishbone/strut/solid-axle mechanism, CV joints, camber, bump steer or a BeamNG node/beam chassis. Existing imported artwork returns with Standard. A future dedicated trophy-truck model can provide more detailed hardpoints/artwork.

## Record a test

Use **Record** in the driving HUD. A recording now includes **suspension.csv** alongside the existing vehicle/wheel/settings logs. It contains per-wheel travel and corrected compression, remaining bump/droop, estimated static sag, spring/damper values, bump-stop model force, airtime, landing speed, landing and bottoming episode counters, lateral wheel offset and approximate proper body acceleration in g.

Test in this order: stationary settling, a low single-wheel obstacle, repeated bumps, a small jump, then larger landings. Mark the attempts if convenient. Compare left/right wheel loads, remaining bump travel, body acceleration and settling; a car remaining invincible does not prove the landing was gentle.

`wheels.csv` retains native VP diagnostics for compatibility. Its raw contact depth/compression omit the adjustable collider-center reference. Use **suspension.csv** for the long-travel kit's actual stroke. Forces in the new table are **kN**, damping **kN·s/m**, mass **tonnes**, distance **metres**, speed **m/s**. The schema is 10; adjacent native contact/force values may lag commands by a physics step.

This is a runtime update. The converter executable, bundled runtime and portable package are unchanged. No third-party parts/assets are included.
