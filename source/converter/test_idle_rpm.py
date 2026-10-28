"""High-idle imports retain their source calibration and reject invalid metadata."""
import unittest,os,json,re
from pathlib import Path
from kdaconvert.automation import validate_idle_rpm
class IdleRpmTests(unittest.TestCase):
 def test_racing_idle_is_preserved(self):self.assertEqual(validate_idle_rpm(1700,12000),1700)
 def test_supported_bounds(self):
  for value in (500,900,1500,2500,5000):self.assertEqual(validate_idle_rpm(value,12000),value)
 def test_invalid_values_rejected(self):
  for value in (499,5001,float('nan'),float('inf'),True,'1700',None):
   with self.subTest(value=value),self.assertRaises(ValueError):validate_idle_rpm(value,12000)
 def test_idle_below_limiter(self):
  for value in (1700,2000):
   with self.assertRaisesRegex(ValueError,'below'):validate_idle_rpm(value,1700)

 def test_real_extreme_export_keeps_idle_and_seven_gears(self):
  root=os.environ.get('KDA_HIGH_IDLE_PACKAGE')
  if not root:self.skipTest('Provide the converted high-idle export for this integration check')
  folder=next((Path(root)/'GameData/KDAVehicles/Vehicles').iterdir());m=json.loads((folder/'vehicle.json').read_text())
  self.assertEqual(m['engine']['idle_rpm'],1700);self.assertEqual(len(m['gearbox']['ratios']),7)
  text=(folder/'Parts/body.cfg').read_text();self.assertEqual(float(re.search(r'idleRpm = ([0-9.]+)',text)[1]),1700)
  self.assertIn('forwardGears = 7',text);self.assertIn('preserveImportedRatios = true',text)
