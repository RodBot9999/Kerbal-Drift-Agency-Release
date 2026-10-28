# KDA rechargeable nitrous

Open Garage / Tune / Nitrous. Select Installed, choose a 25-300 hp shot and one of three bottles, then Apply and Save Car. Hold N by default; click HOLD KEY / CHANGE then press a keyboard key to rebind. Escape cancels binding. The engine also exposes Nitrous (hold key) for KSP custom action groups. Group activation is momentary, never latched.

Small / Medium / Large hold 6 / 12 / 24 seconds at 100 hp. Higher shots empty faster; lower shots last longer. Normal refill from empty takes 30 / 60 / 120 seconds. Sustained grounded drifts (two motor-wheel contacts, speed above 15 km/h, slip angle 12-70 degrees for at least half a second) refill three times faster. Release the key to recharge; holding an empty bottle does not cycle free shots.

Power needs a running fueled engine, Drive, accelerating throttle, at least 1500 rpm, a usable driven wheel, no shift and no limiter. Only the active car accepts input. Pausing, map view, focus loss and time warp stop activation; no recharge during warp. Bottle charge persists with the vessel. Existing cars default to Off. No changes to baseline engine curves or standard driving modes. Added torque passes through existing transmission, axle distribution and wheel traction control. Bottles are arcade capacity, not physical mass or LiquidFuel consumption.

The existing exhaust outlets emit blue nitrous flames while spraying (respecting their flamesEnabled switch); cars without exhaust outlets need a KDA exhaust marker for that visual effect. One original synthesized spray loop per engine follows ship audio volume. HUD displays charge/spray/drift recharge. vehicle.csv appends nitrous installed/active/charge/shot/bottle/recharge columns; prior columns retain their order.

Nitrous is a shared runtime feature; it needs no car reconversion or converter change specifically for nitrous.
