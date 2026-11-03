# KDA Garage

Install KDA Vehicles, Kerbal Konstructs and Custom Pre Launch Checks separately. Select **KDA Garage** in the KK Buildings catalog and save the placement. Its catalog identifier is `KDAGarage`, with model `Garage/garage.mu`. Geometry is upright on local +Y; the door faces +Z. Rotate the heading to face your driveway, then set the desired scale.

## Enter and tune

From the space-center overview, click the highlighted building and choose ENTER GARAGE, or use the KDA badge in the mod toolbar and choose GO TO KDA GARAGE. The same toolbar entry is available from the SPH/VAB and keeps the car already being edited. Cars / Build / Tune uses the separate KDA motorsport workshop interior. Without a placed KK garage the toolbar opens the editor overlay instead. The exterior door remains closed; there are no drive-in animations.

**Tune / Tires** adjusts front and rear grip independently. Other Tune sections cover suspension, brakes, exhaust effects and nitrous. Apply changes, then SAVE CAR to retain the named craft. A chassis preset can replace suspension and grip settings; select it before fine-tuning individual values.

## Drive out and return

Load a complete car with a race seat and available Kerbal. Apply or discard drafts. DRIVE OUT saves the garage session and spawns the car facing outward, seven world metres beyond the door. Keep the driveway clear.

To return, stop a landed, unpacked KDA car below 0.75 m/s, with no warp or pause. The entry zone lies 0.25-18 metres outward from the door, within the scaled door half-width plus three metres laterally and under six metres vertically. Side and rear approaches do not trigger entry. Press **F8** when prompted. WorldGarage.cfg changes the return key using a Unity KeyCode name; restart KSP after changing it.

The complete car, applied tuning and fuel are saved before the workshop opens. Its flight record is removed only after the complete craft loads. Crew remain available. EXIT GARAGE saves the session and returns to the overview; SAVE CAR additionally writes the named craft to the SPH collection.

## Player data and upgrades

Garage sessions are stored under `saves/<save>/KDA-Garage/<garage UUID>/`. Current.craft is the session, with recovery craft/vessel snapshots in Recovery/. These files do not replace the original named craft. Keep the placement UUID when upgrading so the same session remains available.

When upgrading a placement from a different catalog identifier, use `source/tools/migrate_garage_placements.py` with that identifier and the saved KK placement file. It writes a separate migrated file targeting `KDAGarage`, preserves UUID/position/orientation/scale, and never overwrites the input. Back up the game and replace the placement only while KSP is closed. A single optional default garage instance from the default KSC location is now included. Other player placements and sessions are excluded. Move the garage using Ctrl+K in KK, or omit Garage/Garage-instances.cfg to retain a manual instance without duplication.

For an upgrade, back up the garage building directory and KK placement outside GameData. Install the current building directory, retain any custom WorldGarage.cfg return key, and use the migrated placement in place of its input (not alongside it). Preserve the UUID so the workshop session remains available.
