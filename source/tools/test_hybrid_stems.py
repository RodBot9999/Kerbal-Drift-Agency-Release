"""Verify independent tuning tracks reconstruct the current engine mix."""
from pathlib import Path
import hashlib, importlib.util, json, unittest, wave
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('builder',ROOT/'source/tools/build_hybrid_audio.py')
builder=importlib.util.module_from_spec(spec);spec.loader.exec_module(builder)
class StemTests(unittest.TestCase):
 def test_family_idle_bodies_and_nonuniform_cycles(self):
  for family in builder.FAMILIES:
   parts,actual,cycles=builder.synth(family,1250,False,stems=True)
   body=parts[0];power=abs(np.fft.rfft(body))**2;hz=np.fft.rfftfreq(len(body),1/builder.RATE)
   self.assertLess(float((power*hz).sum()/power.sum()),300,family)
   rms=np.array([np.sqrt(np.mean(chunk*chunk)) for chunk in np.array_split(body,cycles)])
   self.assertGreater(rms.std()/rms.mean(),.008,family)
   self.assertAlmostEqual(actual,120*cycles*builder.RATE/len(body),places=9)
 def test_six_cylinder_families_have_different_body_signatures(self):
  signals={name:builder.synth(name,1250,False,stems=True)[0][0] for name in ('i6','v6','boxer6')}
  for first,second in (('i6','v6'),('i6','boxer6'),('v6','boxer6')):
   self.assertLess(abs(np.corrcoef(signals[first],signals[second])[0,1]),.96)
 def test_cross_plane_idle_has_low_body_and_cycle_variation(self):
  for rpm in (750,1250):
   parts,_,cycles=builder.synth('v8_cross',rpm,False,stems=True)
   signal=sum(part*gain for part,gain in zip(parts,(1.7,.95,1.6)))
   power=abs(np.fft.rfft(signal))**2;hz=np.fft.rfftfreq(len(signal),1/builder.RATE)
   self.assertGreater(power[(hz>35)&(hz<240)].sum()/power.sum(),.75)
   self.assertLess(power[hz>800].sum()/power.sum(),.02)
   rms=np.array([np.sqrt(np.mean(chunk*chunk)) for chunk in np.array_split(parts[0],cycles)])
   self.assertGreater(rms.std()/rms.mean(),.025)
 def test_stems_reconstruct_generated_mix(self):
  for family in builder.FAMILIES:
   for rpm in builder.RPMS:
    for loaded in (False,True):
     combined,actual,cycles=builder.synth(family,rpm,loaded)
     stems,stem_rpm,stem_cycles=builder.synth(family,rpm,loaded,stems=True)
     self.assertEqual((actual,cycles),(stem_rpm,stem_cycles))
     np.testing.assert_allclose(sum(stems),combined,atol=1e-12,rtol=0)
     for part in stems:
      self.assertTrue(np.isfinite(part).all());self.assertLess(abs(part).max(),.98)
      self.assertEqual(len(part),len(combined))
 def test_published_stems_and_reconstruction(self):
  root=ROOT/'GameData/KDAVehicles/Sounds/Hybrid';bank=json.loads((root/'bank.json').read_text())
  self.assertEqual(len(bank['tuning_stems']),528)
  for entry in bank['tuning_stems']:
   path=root/entry['path'];self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(),entry['sha256'])
   with wave.open(str(path),'rb') as stream:
    self.assertEqual((stream.getnchannels(),stream.getsampwidth(),stream.getframerate(),stream.getnframes()),(1,2,22050,entry['frames']))
  def samples(path):
   with wave.open(str(path),'rb') as stream:return np.frombuffer(stream.readframes(stream.getnframes()),dtype='<i2').astype(float)
  for entry in bank['files']:
   family,name=entry['path'].split('/')
   parts=[samples(root/'stems'/family/stem/name) for stem in ('body','pulses','texture')]
   self.assertLessEqual(abs(sum(parts)-samples(root/entry['path'])).max(),3)
if __name__=='__main__':unittest.main()
