# Rover drive-by-wire ECU

The **KDA Rover Drive-by-Wire ECU** is a rover-only stability controller. It is
one surface-mounted part in the stock editor's **Control** category; search
for `KDA ECU`. It has its own model and DLL and requires the KDA base runtime
and [HarmonyKSP](https://github.com/KSPModdingLibs/HarmonyKSP/releases/tag/2.2.1.0).
No external mods are bundled.

## Setup

1. Use at least four stock rover wheels, with a steering-capable front wheel
   on each side. Extra driven axles are supported. Keep the rover's normal
   command seat/probe core and power supply.
2. Attach the ECU flat, labelled top up and arrow forward. Its reference axes
   must agree with the rover's normal forward driving direction.
3. Open **ECU setup / wheel groups**. Auto selects the foremost eligible
   steering-capable wheel on each side. Front/Rear groups allow manual
   assignment; Exclude removes a wheel from front-pair selection.
4. Select **Unstable** or **Normal** in the ECU's right-click menu,
   set the individual aids/assistance/cornering allowance, and save the craft.
5. Launch and check the steering direction gently before high-speed testing.
   The ECU exposes just **Toggle ECU / stability control** to action groups.
   Stock KSP collision and autostrut actions remain stock actions.

**Exactly two front wheels steer while enabled, one on each side.** Other
steering-capable wheels are temporarily disabled, including rear steering
configured in the editor. Extra wheels keep their native drive settings. ECU
OFF restores their original steering enabled/disabled flags and steering
angle, response and auto-adjust settings. ON reapplies the selected front pair.
A missing eligible pair leaves the ECU in standby.

The ECU applies one physical steering-angle target to the front pair just
before VPP integrates steering, accounting for mirrored collider axes. Both
front wheels use the smaller available lock limit. This uses parallel front
steering, rather than KSP's per-wheel command-pod/centre-of-mass turn-centre
calculation. Rear-wheel steering is held at zero. Direct keyboard steering
cannot overwrite the ECU target while enabled; its original flag and servo
response are restored when the ECU releases the wheels. Invalid collider-axis
alignment leaves that wheel centred. Native KSP driving verification is still
required after installing an update.

Wheel grouping is editor-only. The setup window contains no ECU mode selector,
drive overrides or powertrain tuning. All propulsion remains native to the rover.
Old saved ECU selections no longer activate any conversion behaviour; the
module keeps the current part ID and retains compatible wheel grouping/profile
fields. Newly saved ECU configuration contains only current rover settings.

## Profiles

| Profile | Behaviour |
| --- | --- |
| **Unstable** | Road-style steering/brake assistance with early front-brake relief when nose-down pitch/rate and rear-load loss indicate an impending nose-over. |
| **Normal** | Race-style recovery with a fixed **5-degree sideslip allowance**. Pitch support starts only after a rear wheel actually loses its supporting load/contact, with nose-down evidence and front contact during forward braking. Relief is limited; it may help but does not guarantee against flipping. |

Normal has no predictive rear-load-percentage trigger: initial intervention
requires an effectively zero rear-wheel load (0.0001 kN numerical tolerance),
or lost contact. Pitch is measured against the contact-normal reference.
Pitch support reduces front brake pressure only, with a short release/recovery
hysteresis. It adds no forces, body rotation or engine power cut. Reverse,
low-speed and airborne steering retain normal driver input.

The ECU retains each wheel's original steering lock. Existing saves using the
removed third profile load as Normal; obsolete angle-kit settings are ignored.
Save the craft again to retain the current settings.

## What the ECU controls

- Adaptive steering and stability recovery using KDA's existing numerical aids.
- Selective native-wheel braking for yaw/wheelspin support and ABS.
- Early/reactive nose-over brake relief according to profile.
- Temporary steering enable/response settings, restored when switched off.

It contains no engine, induction, gearbox, fuel system, drive-layout selector,
drive overrides, suspension tuning or grip tuning. It never replaces native
motors, consumes propulsion fuel, changes tire coefficients, or alters native
springs, damping, travel and ride height. There is no handbrake or artificial
righting torque. **Cornering allowance** runs from **60% to 160%**, in 5%
steps. **100%** uses the previous default conservative baseline: the smaller
of 0.65 g and an approximate track/CG rollover budget. Lower percentages reduce
allowed cornering; higher percentages raise it. The multiplier is applied
after the baseline calculation so it does not get clipped by that same
baseline. The effective budget is still displayed in g. This alters steering
allowance, not tire grip or suspension; higher values may permit a rollover.
Older craft with only the absolute-g setting start at 100%; save the craft to
retain the new percentage. Local gravity determines the steering-angle limit.

The separate Automation-to-KSP converter application and existing KDA cars are
unchanged. Vessels with KDAEngine/KDAController are bypassed to avoid stacking
controllers. With multiple enabled ECUs, the lowest flight-ID ECU owns the
active rover; one ECU per rover is recommended. Switching vessels, packing,
removing the ECU, disabling it or changing wheel topology releases its wheel
claims. Only registered wheels are affected by the physics hook.

Supported wheels use KSP 1.12.5's stock ModuleWheelBase / Vehicle Physics Pro
path. Other wheel frameworks are not claimed compatible. The plug-in compiles
against the installed kda13 and development KDA base runtimes. Native handling,
GUI and save/reload review remains necessary; managed tests do not establish
in-game performance or handling quality.

## Build

```powershell
python source/ecu/build_model.py
./source/ecu/build.ps1
./source/ecu/test.ps1
```

Source: `source/ecu/`. Installable component:
`GameData/KDAVehicles/Addons/ECU/`. Installing this folder does not replace the
base KDA runtime. The original ECU model is 504 triangles; the model/label and
collider are unchanged by the rover-only cleanup. Existing asset-license
scope remains applicable.
