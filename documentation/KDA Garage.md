# KDA Garage — tools, collection, analysis and cinematic look

## Open and return

Click the KDA garage badge in KSP's mod toolbar, then **Go to KDA Garage**. If a KK garage is placed, this opens its workshop with your current editor car and uses its driveway for launching. Otherwise it opens the garage overlay. The floating editor shortcut has been removed.

**Return to SPH** closes the workshop/overlay and keeps the car in the stock editor. A workshop also retains **Exit Garage** to save the session and return to the Space Center. In the VAB the return control is labelled **Return to VAB**. Apply or discard workshop tuning previews before returning. The workshop has matte charcoal/light-gray checkerboard tiles, bright LED lighting and a neon Kerbal Drift Agency sign.

In **Build**, use WASDQE to rotate a selected part, with the stock fine-rotation modifier. Keep the pointer inside the car view; text fields and dialogs block rotation. Right-drag orbits the camera and scrolling zooms.

## Car changes and header

The garage remains visible while loading a different car, using a loading label instead of closing the interface and waiting to reopen it. Collection thumbnails and showroom rotation preference are retained. Craft editing commands are temporarily unavailable during the swap. KSP construction can still pause animation/input briefly on its main thread. Recovery copies and missing-parts checks remain in place; a failed load releases the loading state and shows an error.

The header includes the Sparky mascot beside Kerbal Drift Agency, plus **by Roderic Systems** in smaller green lettering. The icon is embedded in the game runtime and has no external asset dependency.

## Long-travel kits

**Tune → Travel / Kit** adds opt-in Rally and Desert hardware, adjustable front/rear travel and static height, a weight/sag spring calculator, progressive bump stops, position-sensitive damping, full-bump/droop/articulation previews, and suspension telemetry. Standard 160 mm remains the default. See [KDA Long Travel](KDA%20Long%20Travel.md) for setup, testing and model limits.

Off-road spring recommendations include a **75% rough-ground load reserve** above the original parked-weight calculation. The sag slider is a ceiling; the actual predicted parked sag is displayed separately. Saved rates do not change automatically: use **Suggest stiffer off-road spring** for each axle, then Apply and Save.

Graphs now have **Spring**, **Damping** and **Body roll** tabs. The Spring chart keeps its force/compression presentation. Damping separates compression and rebound into force charts in newtons, against suspension movement speed in mm/s. A test-speed marker gives numerical readouts; a position slider and **At rest** button show how the long-travel end damping changes through the stroke. These speeds refer to wheel/suspension movement, not road speed.

## Tuning camera and showroom stance

In **Tune**, use the view buttons above the car: **3/4**, **Left**, **Right**, **Front**, **Rear** and **Top**. Side/front/rear views look squarely along the car's axes, making wheel travel and track changes easier to compare. The camera retains perspective rendering for compatibility with visual mods. Right-drag still orbits freely and scroll still zooms; a view button snaps back to that direction. **Frame Car** and toggling graphs retain the selected direction and fit the car to the available space.

**Cars** displays the applied ride height, front/rear pitch, track extension and Rally/Desert hardware at the normal ride pose. Full bump, full droop, articulation and unapplied tuning drafts are confined to Tune. Apply changes before switching tabs, then Save to keep them in the craft file. Loading that saved car also shows its applied stance in the showroom. Collection thumbnails remain saved/static previews and may not depict the current model-only stance.

**Build** restores the native model/pivot positions for seat, door and part-placement tools. Leaving KDA Garage restores the stock interface/camera controls and keeps the **applied** ride height, pitch, width and kit hardware visible in the stock SPH/VAB. Native suspension tweakables and saved-craft loads refresh that presentation without reopening KDA Garage. Full-bump/droop/articulation poses and unapplied drafts do not carry into stock view.

This is cosmetic presentation: craft Part transforms, attachment nodes and physics colliders remain unchanged, so native placement tools retain their normal references. Repeatedly opening/closing the garage does not accumulate offsets. Leaving the editor or destroying the presentation component restores native model/pivot positions and disposes its generated hardware. These changes do not alter suspension physics or vehicle calibration.

## Build tools

Build has a visible toolbar for Place, Move, Rotate and Root, plus Local/World, Symmetry and Angle Snap. These call KSP's native editor tools, so you can select and reposition a seat or doorway marker normally. Keyboard shortcuts 1–4 still work over the car. Native right-click part settings remain available.

Attached parts selected by a gizmo no longer prevent saving or changing garage sections. A genuinely loose part must still be placed or cancelled first. Leaving Build returns the editor to Place and closes its part settings.

## Graph view

The camera now renders at its full native screen resolution and aspect ratio throughout the garage. Panels crop the visible area, and the camera projection/framing centers the car in the remaining space. The graph panel does not resize or stretch the post-processed image. Original camera aspect, projection, viewport, pose and zoom/height limits are restored on exit.

Tune → Show Graphs has two pages:

- **Spring / Damper:** the existing per-axle spring-force and damping curves.
- **Body Roll:** a quasi-static body-roll estimate versus lateral acceleration from 0–1.5 g, including an estimated angle at 1 g.

### What the roll estimate means

This is an editor analysis model, not measured telemetry, a new physics solver, or a prediction of cornering speed or rollover. It derives current craft mass/CG and track/wheelbase geometry from the assembled car, uses draft wheel spring/bar settings, excludes the wheel parts from sprung mass, and includes the runtime anti-roll force cap. Kerbin gravity is used.

The model assumes level ground, direct wheel rates, four loaded wheels, and a fixed roll axis. Its roll-axis height slider is explicitly an **analysis assumption**; the export does not supply that value. Moving the slider never changes the car. Draft ride-height heave is included in the estimated CG height relative to the wheel contact plane.

The equation balances elastic restoring torque against lateral and gravitational sprung-mass moments. Per-axle linear stiffness is `(spring / 2 + bar) * track²`, in kNm/rad with spring/bar in N/mm (= kN/m). Finite-angle spring and anti-roll moment arms are used. Bar force calls the same `SuspensionCore.CoupledBar` used at runtime. An approximate loaded spring reference uses weight/spring sag plus the native 10%-of-travel preload allowance (16 mm for Standard). Long-travel roll analysis also uses its load cap and stops before bump-stop engagement. The curve stops when its loaded-wheel or compression-travel assumption fails.

It excludes tire compliance, geometric load transfer, suspension motion ratios, aero/downforce and transient damping. A stiffer bar or spring changes this estimate; a damper change does not change steady-state roll. For actual roll and rollover behavior, record driving telemetry. The general sprung-body roll framework is consistent with [vehicle body roll models](https://www.mathworks.com/help/vdynblks/ref/vehiclebody6dof.html); the garage approximation and its limits are specific to KDA.

## Existing chassis presets

Tune now exposes the same **Road / Sport / Race chassis presets** previously available in the native right-click menu. They apply immediately and include their existing suspension and tire-grip multiplier values. The current implementation uses Road 0.75×, Sport 1.15× and Race 2× grip, exactly as the existing preset code does. Apply or discard drafts first.

These are chassis presets, separate from the SC driving mode. Applying one does not change Road/Sport/Race/Drift driving mode or retune its controller. No preset values or runtime physics equations are changed by this update.

## Cars collection

Each card has a **Delete Car** button. Its confirmation shows the car name and exact saved `.craft` path, including whether it is in your save or the game's included Ships folder. **Cancel** leaves it untouched; **Delete Craft File** permanently removes that one file and its collection card. If it is the currently loaded file, confirmation also discards its unsaved edits and unloads the car. The garage stays open and empty until you load another car; Save and Crew / Launch are disabled while empty. Deleting a different saved copy leaves your loaded car alone, even when both have the same display name. Installed vehicle parts remain available.

If the file changes after the confirmation opens, select it again to review the new version. Missing, redirected or inaccessible files produce an error instead of deleting another car. Only craft files from this game's scanned save/included craft folders are eligible.

Cars has a compact horizontal selector with actual car thumbnails and full saved car names below. It loads existing KSP thumbnails when present; otherwise it renders a preview from prefab meshes and craft poses, without creating PartModules or simulating the car. A specifications panel on the right shows the currently loaded car's engine, drive, mass, imported peak power/torque, transmission, front weight, wheelbase and track.

Power/torque include the complete exported boost map. The stored base torque has had boost removed, so the garage restores the combined pressure once, including twincharged engines. The Rabbit's source rating is approximately **409 kW / 549 hp and 606 Nm**. Naturally aspirated ratings remain unchanged.

Select **Cars → Power Graph** to see power (kW, left axis) and torque (Nm, right axis) against engine RPM. The graph uses the same full-boost calculation as the specifications. These are full-load source crank ratings before drivetrain losses, not a live dyno or top-speed prediction. Weight/balance are calculated from the assembled editor craft. The collection still scans only this save and the included craft. Selecting a car retains the confirmation and recovery-copy behavior.

## Exhaust sound

Select **Tune → Sound / FX → Straight pipe**, then **Apply to car** and **Save Car**. The Standard option retains the original mix. Straight pipe raises engine/exhaust source volume by up to 40%, capped at the normal AudioSource volume limit. It affects imported and synthesized engine loops and exhaust shift pops; turbo and BOV keep their existing separate volume control. Your engine volume slider and KSP master volume still apply, including mute.

The choice is saved in the craft, and unapplied choices can be discarded. Hear it when driving. This setting only changes sound: no power, torque, boost, fuel use, engine braking, suspension or handling changes.

## Pops, bangs and flames

In **Tune → Sound / FX**, choose **Stock**, **Crackle** or **Flame**. Stock at 1x retains the original occasional high-RPM lift/downshift/boosted-upshift event timing. Crackle extends overrun with mostly short pops; Flame favors larger bangs and visible bursts. These are cosmetic tunes, not anti-lag or fuel-map modifications.

- **Pops / bangs frequency:** 0–3x; zero disables afterfire events.
- **Pops / bangs sound:** independent 0–1 volume, also respecting engine and KSP ship volume.
- **Flame size:** 0.25–3x, scaling width and reach.
- **Flame duration:** 0.5–3x, scaling particle lifetime.

All outlets use one event stream per engine, with distinct synthesized pop and bang sounds. Multiple tailpipes do not multiply the sound. Sound is available even when an outlet's flame visibility is disabled. Without an installed KDA exhaust outlet there is no afterfire emitter. The sliders are drafts until **Apply to car**, and are persisted by **Save Car**. Scroll this section to reach the lower controls.

## Service braking power

**Tune → Brakes** adjusts normal service brake torque from 0–200%, with 100% retaining original strength. In Drift driving mode, B remains front-only. Engine drag, selective SC/TCS brake torque, rear handbrake and burnout hold retain their original absolute torque. ABS continues to release and recover the service brakes. Apply and Save Car to persist this editor-only tuning.

## Drift chassis preset

Select **DRIFT** in Tune's chassis preset row. It immediately applies front/rear springs of 190/155 N/mm, anti-roll rates of 45/30 N/mm, −8 mm height, 1.15x grip, 0° front and 0.05° rear toe-in, and its own damping values. It uses the existing 160 mm travel. Road, Sport and Race preset values are unchanged. Chassis selection does not change the SC driving mode, steering lock, LSD or engine calibration.

## Garage input protection

Stock part-list 3D icons are hidden and their hover/pick input is locked. Native raycasters behind the KDA interface are disabled, and an invisible input shield blocks clicks behind its panels. Build's car workspace remains available for native placement, movement, rotation and part settings. Closing the garage restores the original stock icon visibility and raycasters and removes the shield.

## Cinematic color filter

**LOOK: CINEMATIC / ORIGINAL** toggles a garage-only color grade: mild cool balance, stronger contrast, a small saturation/exposure lift and ACES tone mapping. It uses an optional reflection bridge to the TUFX in the selected KSP installation. KDA does not acquire a hard TUFX assembly dependency or bundle another mod. Without TUFX the original image remains and the button explains the requirement.

The grade affects the garage camera image, not saved paint, materials or UI. It creates a separate temporary volume/profile, never edits the user's TUFX profile, and removes the volume on leaving the garage. It is not a fix for material/reflection defects.

## Tires, nitrous and installation

Tune / Tires edits front and rear KDAGrip independently from chassis presets. Tune / Nitrous enables the optional rechargeable shot, three bottle sizes and held key binding. Nitrous is the explicit engine-power addon; suspension, grip, brake and cosmetic exhaust changes use their existing modules. Standard suspension remains 160 mm; Rally and Desert kits add adjustable travel through the native wheel solver. Select a chassis preset before fine-tuning individual settings.



## Optional default world placement

With Kerbal Konstructs and its dependencies installed, the package includes the
KDA Garage placement used in the default KSC location. It is relative to `KSC_Builtin` on
Kerbin: position `-396.634735, 24.5667, 372.811676`, rotation
`0, 150.000214, 0`, scale `2`. These are KK group-relative coordinates.

The location is optional. Press **Ctrl+K** to open the KK editor, select the
garage and move or rotate it. To disable automatic placement while retaining
the model, remove only `GameData/KDAGarageBuilding/Garage/Garage-instances.cfg`.
For an existing manually placed garage, omit this default instance to avoid
duplicates. The normal SPH/VAB garage remains available without KK.
