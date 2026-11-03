"""Original bank integrity and no-recording converter contract."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import wave
from unittest.mock import patch
import numpy as np
from kdaconvert.hybrid_audio import metadata, config, write

SOURCE = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('kda_bank_builder', SOURCE / 'tools/build_hybrid_audio.py')
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)


class HybridAudioTests(unittest.TestCase):
    def test_presets_and_no_recordings(self):
        for layout, family in [('I3','i3'),('I4','i4'),('I5','i5'),('I6','i6'),('V6','v6'),('V8','v8_cross'),('V10','v10'),('V12','v12'),('B4','boxer4'),('B6','boxer6')]:
            a = metadata(dict(layout=layout, displacement_l=5.25))
            self.assertEqual(a['family'], family)
            self.assertFalse(a['exported_recordings_copied'])
            self.assertEqual(a['samples'], [])
            self.assertEqual(a['effects'], [])
            self.assertIn('engineVoice = 0', config(a))

    def test_unknown_and_invalid_displacement(self):
        for value in [float('nan'), float('inf'), -2, 999, True, '2\nname = bad']:
            a = metadata(dict(layout='V8\nname = bad', displacement_l=value))
            self.assertEqual(a['layout'], 'I4')
            self.assertEqual(a['displacement_l'], 0)
            self.assertNotIn('bad', config(a))

    def test_no_export_audio_file_access(self):
        with tempfile.TemporaryDirectory() as tmp:
            dest = Path(tmp);(dest/'Parts').mkdir()
            (dest/'Parts/body.cfg').write_text('PART\n{\nname = test\n}\n')
            manifest = dict(engine=dict(layout='V8',displacement_l=5.25), warnings=[])
            # Export input is deliberately nonexistent; even a blend JSON/WAV
            # lookup would fail. No engine recordings may be opened or copied.
            with patch('wave.open', side_effect=AssertionError('Recording accessed')):
                write(manifest, dest/'nonexistent-source', dest)
            self.assertEqual(manifest['audio']['type'], 'kda.hybrid')
            self.assertIn('voiceLayout = V8', (dest/'Parts/body.cfg').read_text())
            self.assertEqual(list(dest.rglob('*.wav')), [])

    def test_generated_signal_safety_and_periodicity(self):
        for family in builder.FAMILIES:
            for rpm in builder.RPMS:
                for loaded in [False, True]:
                    signal, actual, cycles = builder.synth(family, rpm, loaded)
                    self.assertTrue(np.isfinite(signal).all())
                    self.assertLessEqual(abs(signal).max(), .681)
                    self.assertGreater(np.sqrt(np.mean(signal*signal)), .04)
                    self.assertLess(abs(signal.mean()), 1e-8)
                    self.assertAlmostEqual(actual * len(signal) / (120*builder.RATE), cycles, places=9)
                    # The loop seam must not introduce an anomalous step.
                    seam=abs(signal[0]-signal[-1]);steps=abs(np.diff(signal))
                    self.assertLess(seam, max(.02, np.quantile(steps,.999)*1.2))

    def test_reproducible_distinct_voices(self):
        a = builder.synth('v8_cross', 3200, True)[0]
        b = builder.synth('v8_flat', 3200, True)[0]
        self.assertTrue(np.array_equal(a, builder.synth('v8_cross',3200,True)[0]))
        self.assertGreater(np.sqrt(np.mean((a-b)**2)), .035)

    def test_committed_bank_budget_and_headers(self):
        root=SOURCE.parent/'GameData/KDAVehicles/Sounds/Hybrid'
        manifest=json.loads((root/'bank.json').read_text())
        frames=0
        for entry in manifest['files']:
            with wave.open(str(root/entry['path']),'rb') as stream:
                self.assertEqual((stream.getnchannels(),stream.getsampwidth(),stream.getframerate()),(1,2,22050))
                self.assertEqual(stream.getnframes(),entry['frames'])
                frames+=stream.getnframes()
        self.assertEqual(len(manifest['files']),176)
        self.assertLess(frames*4, 13*1024*1024)


if __name__=='__main__': unittest.main()
