import sys,unittest,pathlib
sys.path.insert(0,'converter')
from kdaconvert.automation import inspect
from kdaconvert.characteristics import numeric
from fixture_paths import export_fixture
class PhysicsTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.c=inspect(export_fixture('analysis-corvette'),'test')[0];cls.l=inspect(export_fixture('analysis-leopard'),'test')[0]
 def test_source_differences(self):
  self.assertEqual(self.c['engine']['layout'],'V8');self.assertEqual(self.l['engine']['layout'],'I4')
  self.assertEqual(self.c['gearbox']['type'],'sequential');self.assertEqual(len(self.c['gearbox']['ratios']),6);self.assertEqual(len(self.l['gearbox']['ratios']),5)
  self.assertAlmostEqual(self.l['drive']['rear_torque_fraction'],.6,places=5)
  self.assertEqual(self.c['engine']['limit_rpm'],10400);self.assertEqual(self.l['engine']['limit_rpm'],10600)
  self.assertAlmostEqual(self.l['engine']['max_boost_bar'],.77)
 def test_torque_reconstruction(self):
  for car in [self.c,self.l]:
   e=car['engine'];p=dict(e['boost_curve_bar']);net=dict(e['full_load_torque_curve'])
   for rpm,base in e['torque_curve']:self.assertAlmostEqual(base*(1+p[rpm]),net[rpm],places=6)
  self.assertAlmostEqual(self.c['engine']['peak_power_kw']/ .7457,944,delta=2)
 def test_aero_and_tires(self):
  self.assertAlmostEqual(self.c['aero']['front_downforce_n']+self.c['aero']['rear_downforce_n'],2876.17)
  self.assertAlmostEqual(self.l['aero']['front_downforce_n']+self.l['aero']['rear_downforce_n'],139.75745)
  cr=next(w for w in self.c['wheels'] if w['corner']=='RR');lr=next(w for w in self.l['wheels'] if w['corner']=='RR')
  self.assertEqual(cr['width_m'],.375);self.assertEqual(lr['width_m'],.275);self.assertGreater(cr['grip_calibration'],lr['grip_calibration'])
class ExpressionTests(unittest.TestCase):
 def test_expression_safety_and_lua_zero(self):
  self.assertEqual(numeric('$=$zero or 7',{'$zero':0}),0)
  with self.assertRaises(ValueError):numeric('$=__import__("os")',{})
if __name__=='__main__':unittest.main()
