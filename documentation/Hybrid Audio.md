# Hybrid engine audio

Current main version. Runtime **1.0 /
2026-10-28-kda25** and Converter **2.0 / 2026-10-26-kda12** have independent
build counters.

## Player controls

Open **KDA Garage / Tune / Sound / FX**. Engine sound has two controls:

- **Engine volume**: local engine loudness, respecting KSP's ship-volume setting.
- **Exhaust**: Standard, Straight Pipe, Quiet Muffler, Sport, Deep Tone or Race.

Apply, then Save Car. These controls change sound only, not power, boost,
fuel consumption or handling. Engine volume is also available live in flight from the car body
right-click menu, with a 0-100% slider. Exhaust remains garage-only. Advanced
audio controls remain hidden in both stock editor and flight menus.
The separate pops/bangs/flame-kit controls remain under their own heading.

The temporary voice, tone and sound-layer controls, mix reset and tune-export
buttons have been removed. Their saved values remain compatible internally,
so an existing approved mix is not reset. This UI cleanup changes no audio
processing, generated clips, engine-family defaults or saved craft files.
New imports select the engine family automatically from export metadata.

## Installation and existing cars

Install the complete base GameData with its shared sound bank before adding
cars. A DLL alone is insufficient. The converter produces car-only packages
and checks the base ABI/build and required shared-bank hashes. It does not
copy exported engine recordings into new packages.

Existing KDAAudio and KDASampleAudio module names are supported. Older imports
infer engine layout/displacement from the imported engine label; Converter v2
can supply explicit metadata. Runtime sound/UI updates do not require
reconverting an existing compatible car. Old unused recordings may still be
preloaded by KSP even though this player does not use them.

## Engine families and character

The current bank is voicing revision 5. It contains original mathematical
synthesis and deterministic noise, not input recordings or measured impulse
responses. These are sound-design presets rather than inferred crank/header
measurements. Auto V8 defaults to cross-plane; Boxer 4 uses an unequal-header
preset. Unknown layouts fall back to Inline 4.

| Family | Character |
| --- | --- |
| Inline 3 | Low three-cylinder throb |
| Inline 4 | Tight growl with a dry edge |
| Inline 5 | Syncopated warble |
| Inline 6 | Smooth rounded hum |
| V6 | Coarser bank-separated growl |
| Cross-plane V8 | Revision 4 low rumble retained |
| Flat-plane V8 | Even sharper bark over a low body |
| V10 | Layered tenor with a coarse low edge |
| V12 | Dense smooth body, restrained variation |
| Boxer 4 | Unequal-header burble preset |
| Boxer 6 | Rounded opposed-six chatter |

Deeper resonances and bounded cycle variation are baked into the clips. They
retain RPM calibration, average firing rate and original loop durations.
The cross-plane V8 clips remain byte-identical to the installed revision 4.
Tone/muffler filtering reaches the audible body frequencies. Intake, turbo,
blow-off and shift details respond to runtime engine state. Exhaust muffling
is separate from turbo/blow-off effects.

## Architecture and verification

The bank has **705 clips**, including **528 separate tuning stems**, and about
**46.37 MiB** of decoded mono PCM. Clips are shared across cars. Playback uses
at most **12 combustion sources per audible car** and **four audible cars**;
intake and effects use additional bounded sources. There is no custom audio
callback, per-car waveform synthesis or per-frame PCM duplication.

Unity AudioSources are created before the tone filter. Incomplete players
cannot enter playback. Pause, map view and time warp stop playback; silent
sources are stopped rather than rescheduled continuously.

The managed playback harness passed **1,448 checks**. Five bank tests check
headers/hashes, stem reconstruction, low-frequency body/cycle variation and
different six-cylinder signatures. A binary audit confirms that the final UI
cleanup preserves non-garage method bodies and persistent saved mix fields.
These checks do not establish native KSP FPS or guarantee perceived realism.

Source: `source/plugin/HybridAudioCore.cs`, `KDAHybridAudio.cs`, audio modules,
`KDAGarageExhaust.cs`, and `source/tools/build_hybrid_audio.py`.
Original KDA audio is licensed under CC BY 4.0. See the asset terms and credits
in the repository root.

Flight volume changes are saved with the vessel. They do not automatically
rewrite the saved editor craft; use Garage / Apply / Save Car for its defaults.
