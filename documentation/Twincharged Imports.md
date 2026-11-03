# Twincharged imports

The converter supports NA, Turbo, Supercharged and TwinCharged source engines. Imported transmission and induction remain fixed to their exported types.

## Calibration and response

The adapter reads the full-throttle torque and absolute pressure columns, subtracts exported friction, and divides torque by the pressure ratio. The runtime reapplies the combined pressure once. The steady full-load torque curve is preserved; neither two compressor multipliers nor a generic extra supercharger loss are added on top of it.

Twincharged exports must contain both selected compressor sections. Their exported supercharger pressure ceiling (PSI converted to bar), clutch engagement/disengagement RPM, and turbo inertia/size/pressure-rate settings are retained. The runtime supplies a capped mechanical share before turbo spool, smoothly fades it near clutch disengagement, and lets the turbo supply the remaining pressure. This is a transient approximation using source calibration, not an execution of BeamNG's compressor simulation. Pure imported superchargers use their pressure map without turbo lag or replacement by the generic 0.55-bar tuning preset.

## Rabbit Rally X regression car

- Source: user's `rodbot_99_rabbit_rally_x.zip`; it is tested externally and is not included as another playable repository car.
- Engine: 1.723 L I4, twincharged, 8,000 rpm limit.
- Export net peak: 606.48 Nm, approximately 409.28 kW.
- Peak combined boost: 2.7 bar gauge.
- Roots supercharger: 38.13 PSI pressure ceiling, approximately 2.629 bar; clutch disengages at 3,400 rpm.
- Gearbox: six-speed sequential, final drive 4.52.
- AWD: nominal 50% front / 50% rear, before runtime differential redistribution.

The numeric compressor regression fixture retains its source notice beside it. Parser and engine-core tests cover full-load reconstruction, required compressor data, pure supercharger imports, low-RPM response, compressor handoff, boost-controller scaling and throttle release. Current release checks are recorded in release-manifest.json.

Runtime and converter installation are separate. The converter verifies the current base framework and installs only the car and craft; it never upgrades the runtime. Install the base GameData separately when its runtime or crew needs updating.
