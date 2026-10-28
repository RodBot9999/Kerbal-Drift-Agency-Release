"""Generate original, periodic KDA combustion-loop banks. No recordings as inputs.

GPL-3.0-only for this generator. Output audio has separate asset provenance.
"""
from pathlib import Path
import argparse, hashlib, json, math, wave
import numpy as np

RATE = 22050
RPMS = (750, 1250, 2000, 3200, 5000, 7500, 11000, 16000)
FAMILIES = {'i3': 3, 'i4': 4, 'i5': 5, 'i6': 6, 'v6': 6,
            'v8_cross': 8, 'v8_flat': 8, 'v10': 10, 'v12': 12,
            'boxer4': 4, 'boxer6': 6}
VOICING = dict(version=5, name='distinct-deeper-families', body=1.0,
               pulse_loaded=.08, pulse_coast=.05,
               texture_loaded=.12, texture_coast=.075,
               idle_pulse_reduction=.65, idle_dispersion=.55,
               revised_families=list(FAMILIES), idle_variation=.23,
               cross_plane_reference='Retains revision 4 clips unchanged')

# Sound-design presets, not inferred crank/header measurements. Different
# resonances, bank balance and bounded cycle variation distinguish layouts
# with the same cylinder count while preserving their average firing rate.
CHARACTERS = {
    'i3': dict(character='Low three-cylinder throb', formants=.84, floor=35, cutoff=(1450,1950), center=105, low_gain=.85, bank=1, variation=.15, jitter_ms=.55, rhythm=(2,5,9), texture=1600),
    'i4': dict(character='Tight four-cylinder growl with a dry edge', formants=.90, floor=40, cutoff=(1550,2100), center=135, low_gain=.60, bank=1, variation=.085, jitter_ms=.28, rhythm=(3,7,11), texture=2000),
    'i5': dict(character='Syncopated five-cylinder warble', formants=.86, floor=35, cutoff=(1550,2050), center=125, low_gain=.75, bank=1, variation=.13, jitter_ms=.48, rhythm=(2,5,7), texture=1800),
    'i6': dict(character='Smooth rounded six-cylinder hum', formants=.88, floor=38, cutoff=(1600,2200), center=135, low_gain=.70, bank=1, variation=.045, jitter_ms=.15, rhythm=(2,7,11), texture=1850),
    'v6': dict(character='Coarser bank-separated six-cylinder growl', formants=.82, floor=32, cutoff=(1450,1950), center=115, low_gain=.90, bank=.79, variation=.11, jitter_ms=.42, rhythm=(3,5,8), texture=1700),
    'v8_flat': dict(character='Even, sharper eight-cylinder bark over a low body', formants=.92, floor=38, cutoff=(1650,2300), center=145, low_gain=.55, bank=.93, variation=.07, jitter_ms=.23, rhythm=(3,7,10), texture=2100),
    'v10': dict(character='Layered ten-cylinder tenor with a coarse low edge', formants=.88, floor=34, cutoff=(1600,2250), center=150, low_gain=.75, bank=.86, variation=.075, jitter_ms=.24, rhythm=(2,5,11), texture=2000),
    'v12': dict(character='Dense, smooth twelve-cylinder body', formants=.90, floor=38, cutoff=(1750,2350), center=155, low_gain=.65, bank=.96, variation=.035, jitter_ms=.12, rhythm=(2,7,13), texture=1950),
    'boxer4': dict(character='Unequal-header boxer burble', formants=.78, floor=28, cutoff=(1300,1800), center=95, low_gain=1.0, bank=.80, variation=.14, jitter_ms=.58, rhythm=(2,4,9), texture=1500),
    'boxer6': dict(character='Rounded opposed-six chatter', formants=.84, floor=32, cutoff=(1500,2050), center=125, low_gain=.80, bank=.74, variation=.065, jitter_ms=.25, rhythm=(3,7,9), texture=1750),
}


def unit_rms(data):
    return data / max(np.sqrt(np.mean(data * data)), 1e-12)


def synth(family, rpm, loaded, stems=False):
    cylinders = FAMILIES[family]
    character = CHARACTERS.get(family)
    cycles = max(4, round(.78 * rpm / 120))
    length = round(cycles * 120 * RATE / rpm)
    actual_rpm = 120 * cycles * RATE / length
    base = actual_rpm / 120
    harmonics = np.arange(1, int(8800 / base) + 1)
    hz = harmonics * base
    phases = np.arange(cylinders) / cylinders
    weights = 1 + .035 * np.sin(np.arange(cylinders) * 2.17)
    if family == 'v8_cross':
        banks = np.array([0, 1, 1, 0, 1, 0, 0, 1])
    elif family.startswith('v') or family.startswith('boxer'):
        banks = np.arange(cylinders) % 2
    else:
        banks = np.zeros(cylinders, dtype=int)
    # Complex bank responses reproduce uneven exhaust-bank grouping without
    # copying any measured waveform or impulse response.
    pressure = np.zeros(len(harmonics), dtype=complex)
    for bank in sorted(set(banks)):
        selected = banks == bank
        offsets = phases[selected].copy()
        if family == 'boxer4' and bank == 1:
            offsets += .038  # explicit unequal-header sound-design preset
        pulse = np.exp(-2j * np.pi * harmonics[:, None] * offsets)
        bank_signal = pulse @ weights[selected]
        pipe_scale = 1 if bank == 0 else 1.17
        deep = character['formants'] if character else .83 if family in ('v8_cross', 'boxer4') else 1.0
        centers = np.array([115, 310, 720, 1550, 2950]) * deep * pipe_scale
        widths = np.array([70, 140, 260, 480, 800])
        resonance = np.full(len(hz), .3, dtype=complex)
        for center, width, gain in zip(centers, widths, (.35, 1.0, .65, .36, .18)):
            resonance += gain / (1 + 1j * (hz - center) / width)
        # Loaded combustion is sharper; coast keeps the same pulse phases.
        cutoff = 4200 if loaded else 1450
        shape = 1 / (1 + (hz / cutoff) ** 2)
        shape *= 1 / np.maximum(1, hz / 130) ** .62
        pipe_delay = .00055 if family == 'boxer6' else .00032 if family == 'v6' else .00018
        delay = pipe_delay if bank else 0
        # Keep the irregular cross-plane bank pattern audible instead of
        # summing nearly identical banks into an even, high-order buzz.
        bank_gain = .68 if family == 'v8_cross' and bank else 1
        if character and bank:
            bank_gain = character['bank']
        pressure += bank_gain * bank_signal * resonance * shape * np.exp(-2j * np.pi * hz * delay)
    indices = harmonics * cycles
    valid = indices < length // 2 + 1

    def render(partials):
        spectrum = np.zeros(length // 2 + 1, dtype=complex)
        spectrum[indices[valid]] = partials[valid]
        return unit_rms(np.fft.irfft(spectrum, n=length))

    # The original phase-aligned pressure train made individual firing attacks
    # dominate. Bring the resonant engine body forward while retaining a quiet
    # pulse accent. Dispersion is tied to engine order, so neighboring RPM/load
    # banks share phase relationships rather than choosing arbitrary phases.
    body_band = (hz / (hz + 65)) ** 1.35 / (1 + (hz / 2200) ** 4)
    if family == 'v8_cross':
        body_band = (hz / (hz + 24)) ** .85 / (1 + (hz / (1250 if loaded else 900)) ** 4)
        body_band *= 1 + 2 * np.exp(-((hz - 105) / 85) ** 2)
    elif character:
        body_band = (hz/(hz+character['floor']))**1.05 / (1+(hz/character['cutoff'][int(loaded)])**4)
        body_band *= 1+character['low_gain']*np.exp(-((hz-character['center'])/110)**2)
    idle = np.clip((2000-rpm)/1100,0,1)
    idle = idle*idle*(3-2*idle)
    dispersion = np.exp((.95+VOICING['idle_dispersion']*idle)*1j * (harmonics / cylinders) ** 1.27)
    body = render(pressure * body_band * dispersion)
    if family == 'v8_cross':
        # Baked, smooth cycle-to-cycle variation breaks the organ-like idle.
        # Periodic modulation keeps the loop seamless and adds no live DSP.
        phase = np.arange(length) / length
        variation = (.12*np.sin(2*np.pi*(2*phase+.3)) +
                     .07*np.sin(2*np.pi*(5*phase+.7)) +
                     .04*np.sin(2*np.pi*(9*phase+.1)))
        roughness = idle * (.35 if loaded else 1)
        delay_samples = roughness * .0008 * RATE * (np.sin(2*np.pi*(3*phase+.2)) + .35*np.sin(2*np.pi*(7*phase+.6)))
        body = np.interp(np.mod(np.arange(length)+delay_samples,length),np.arange(length+1),np.r_[body,body[0]])
        body = unit_rms(body*(1+roughness*variation))
    elif character:
        # Modulation is periodic over the existing clip, so every loop retains
        # its original duration. It becomes subtle at load/high RPM rather
        # than turning all families into the same rough V8 idle.
        phase = np.arange(length)/length
        a,b,c = character['rhythm']
        variation = character['variation']*(.60*np.sin(2*np.pi*(a*phase+.3)) + .28*np.sin(2*np.pi*(b*phase+.7)) + .12*np.sin(2*np.pi*(c*phase+.1)))
        roughness = (.25+.75*idle)*(.65 if loaded else 1)
        delay_samples = roughness*character['jitter_ms']*.001*RATE*min(1,(750/rpm)**.7)*(np.sin(2*np.pi*(b*phase+.2))+.3*np.sin(2*np.pi*(c*phase+.6)))
        body = np.interp(np.mod(np.arange(length)+delay_samples,length),np.arange(length+1),np.r_[body,body[0]])
        body = unit_rms(body*(1+roughness*variation))
    pulses = render(pressure)
    pulse_level = VOICING['pulse_loaded' if loaded else 'pulse_coast']*(1-VOICING['idle_pulse_reduction']*idle)
    components = [body * VOICING['body'], pulses * pulse_level]
    signal = components[0] + components[1]
    # A gentle original noise bed adds combustion texture. Its periodic FFT
    # construction avoids a seam; deterministic seed makes assets reproducible.
    seed = sum((i + 1) * ord(c) for i, c in enumerate(family)) + round(rpm)
    rng = np.random.default_rng(seed)
    freq = np.fft.rfftfreq(length, 1 / RATE)
    noise_spec = np.fft.rfft(rng.normal(size=length))
    noise_spec *= (freq / (freq + 180)) ** 2 / (1 + (freq / 2600) ** 4)
    if family == 'v8_cross':
        noise_spec *= 1 / (1 + (freq / 1400) ** 4)
    elif character:
        noise_spec *= 1 / (1 + (freq / character['texture']) ** 4)
    noise = np.fft.irfft(noise_spec, n=length)
    noise /= max(np.sqrt(np.mean(noise * noise)), 1e-12)
    components.append(noise * VOICING['texture_loaded' if loaded else 'texture_coast'])
    signal += components[2]
    # Keep ample headroom for simultaneous engine, intake, turbo and effects.
    gain = (.16 if loaded else .095) / max(np.sqrt(np.mean(signal * signal)), 1e-12)
    gain *= min(1, .68 / max(np.max(np.abs(signal * gain)), 1e-12))
    signal *= gain
    return ([c * gain for c in components] if stems else signal), actual_rpm, cycles


def intake():
    length = RATE
    rng = np.random.default_rng(85173)
    f = np.fft.rfftfreq(length, 1 / RATE)
    s = np.fft.rfft(rng.normal(size=length))
    s *= (f / (f + 450)) ** 3 / (1 + (f / 4300) ** 6)
    data = np.fft.irfft(s, n=length)
    return data * .13 / max(np.max(np.abs(data)), 1e-12)


def wav(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    assert np.isfinite(data).all()
    with wave.open(str(path), 'wb') as stream:
        stream.setnchannels(1); stream.setsampwidth(2); stream.setframerate(RATE)
        stream.writeframes((np.clip(data, -.98, .98) * 32767).astype('<i2').tobytes())


def build(dest):
    inventory = []
    stem_inventory = []
    for family in FAMILIES:
        for i, rpm in enumerate(RPMS):
            for bank in range(2):
                data, actual, cycles = synth(family, rpm, bank == 1)
                path = dest / family / f'rpm_{i}_{bank}.wav'
                wav(path, data)
                parts, _, _ = synth(family, rpm, bank == 1, stems=True)
                for name, part in zip(('body', 'pulses', 'texture'), parts):
                    stem_path = dest / 'stems' / family / name / path.name
                    wav(stem_path, part)
                    stem_inventory.append(dict(path=stem_path.relative_to(dest).as_posix(),
                        frames=len(part), sha256=hashlib.sha256(stem_path.read_bytes()).hexdigest()))
                # Integer-cycle periodicity is more useful than demanding that
                # the last sample equal the first (which flattens the seam).
                inventory.append(dict(path=path.relative_to(dest).as_posix(),
                    rpm=actual, cycles=cycles, frames=len(data),
                    rms=float(np.sqrt(np.mean(data * data))), peak=float(max(abs(data))),
                    seam_step=float(abs(data[0] - data[-1])),
                    sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    wav(dest / 'intake.wav', intake())
    manifest = dict(version=1, generator='source/tools/build_hybrid_audio.py',
        provenance='Original mathematical synthesis; no input recordings, sampled waveforms or recorded impulse responses.',
        sample_rate=RATE, reference_rpms=RPMS, families=FAMILIES, voicing=VOICING, characters=CHARACTERS, files=inventory, tuning_stems=stem_inventory,
        intake=dict(path='intake.wav', sha256=hashlib.sha256((dest/'intake.wav').read_bytes()).hexdigest()))
    (dest / 'bank.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(clips=len(inventory) + len(stem_inventory) + 1, tuning_stems=len(stem_inventory), seconds=sum(x['frames'] for x in inventory) / RATE,
        decoded_mib=(sum(x['frames'] for x in inventory + stem_inventory) + RATE) * 4 / 2**20,
        output=str(dest))))
    return manifest


def preview(path):
    result = []
    for family in ('i4', 'i6', 'v8_cross'):
        # Preview uses the same generated banks, phase-correct pitch and linear
        # crossfades as runtime, with original intake detail added at load.
        seconds = 8
        t = np.arange(seconds * RATE) / RATE
        rpm = np.interp(t, [0, 1, 5.5, 6, 8], [850, 850, 7200, 4200, 1100])
        load = np.interp(t, [0, .9, 1.1, 5.5, 5.6, 8], [.03, .03, 1, 1, .03, .03])
        crank = np.cumsum(rpm / (120 * RATE))
        out = np.zeros(len(t))
        lo = np.clip(np.searchsorted(RPMS, rpm, side='right') - 1, 0, len(RPMS) - 2)
        weight = np.clip((rpm - np.array(RPMS)[lo]) / (np.array(RPMS)[lo + 1] - np.array(RPMS)[lo]), 0, 1)
        for i, ref in enumerate(RPMS):
            blend = np.where(lo == i, 1 - weight, 0) + np.where(lo + 1 == i, weight, 0)
            for bank in range(2):
                data, _, cycles = synth(family, ref, bank == 1)
                pos = np.mod(crank / cycles, 1) * len(data)
                sample = np.interp(pos, np.arange(len(data) + 1), np.r_[data, data[0]])
                out += sample * blend * (load if bank else 1 - load)
        # Match the runtime's idle-relative volume ramp (850 RPM for this demo).
        rise=np.maximum(np.clip((rpm/850-1)/.9,0,1),np.minimum(1,load*2))
        idle_gain=.70+.30*rise*rise*(3-2*rise)
        out *= idle_gain
        out += np.resize(intake(), len(t)) * load * .18 * idle_gain
        fade = np.minimum(1, np.minimum(t / .025, (seconds - t) / .08))
        result.extend([out * fade, np.zeros(RATE // 2)])
    wav(path, np.concatenate(result))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[2] / 'GameData/KDAVehicles/Sounds/Hybrid')
    parser.add_argument('--preview', type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    build(args.output)
    if args.preview: preview(args.preview)
