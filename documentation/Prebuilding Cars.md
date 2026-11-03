# Prebuild a car

In the KDA garage, open **Cars → Prebuild Car**. This lists installed, unlocked KDA car bodies even when your save has no craft files.

- **Complete:** Lizard GT and Rabbit Rally x use the supplied finished craft templates, including wheels, saved seat positions and door markers. Assign a Kerbal before driving.
- **Chassis:** other supported four-wheel imports use their exact wheel attachment nodes. The garage also adds an outward-facing doorway marker, but you must check its position and add a physical KDA seat.
- **Load saved:** if the same car body already appears in a saved craft in this game's SPH or VAB collection, the existing craft is offered instead of generating another copy. Your tuning and seat adjustments are retained.

Prebuilding creates one craft in the current editor's save folder. Confirm loading it to replace the current editor craft; existing unsaved work follows the garage's normal recovery-copy flow. It never overwrites a different craft file automatically.

For other imports, an optional package `KDA_PREBUILD` node with `part` and body-local `doorPosition` values takes precedence. Otherwise named left-door mesh geometry is used when available. Existing exports do not consistently provide an explicit doorway location; without either hint, the marker uses an approximate side position. Always inspect its placement and outward arrow in Build. Missing wheel nodes stop the prebuild rather than creating a broken car.

Included templates are installed under `GameData/KDAVehicles/Prebuilds`, so copying the KDA GameData folders is sufficient. They do not rely on the separate stock Ships folder. Included collection entries are hidden once that car has a saved copy.
