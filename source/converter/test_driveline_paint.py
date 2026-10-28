import unittest,tempfile,json,os
from pathlib import Path
from kdaconvert.driveline_metadata import enrich_active_drive,config
from kdaconvert.materials import Materials
class DrivelineAndPaintTests(unittest.TestCase):
 def part(self,device,kind,controller,offset):return {'powertrain':[['type','name','inputName','inputIndex'],['differential',device,'shaft',1,{'diffType':kind}]],'controller':[['fileName'],[controller,{'differentialName':device,'maxBiasOffset':offset}]]}
 def test_explicit_selected_hardware(self):
  m={'drivetrain':'AWD','drive':{}};order=[('a',self.part('transferCase','lsd','drivingDynamics/actuators/camso_advawd',.95)),('b',self.part('frontDiff','torqueVectoring','drivingDynamics/actuators/activeDiffBias',.2)),('c',self.part('rearDiff','torqueVectoring','drivingDynamics/actuators/activeDiffBias',.3))];enrich_active_drive(m,order,{})
  self.assertTrue(all(x['enabled'] for x in m['drive']['active_hardware'].values()));self.assertIn('importedActiveCenter = true',config(m));self.assertIn('rearVectoringOffset = 0.3',config(m))
 def test_awd_alone_does_not_enable_electronics(self):
  m={'drivetrain':'AWD','drive':{}};enrich_active_drive(m,[],{});self.assertTrue(all(not x['enabled'] for x in m['drive']['active_hardware'].values()))
 def test_passive_lsd_does_not_become_vectoring(self):
  m={'drivetrain':'AWD','drive':{}};enrich_active_drive(m,[('x',self.part('frontDiff','lsd','drivingDynamics/actuators/activeDiffBias',.2))],{});self.assertFalse(m['drive']['active_hardware']['front']['enabled'])
 def test_primary_paint_override_keeps_finish_and_secondary(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d);(p/'info.json').write_text(json.dumps({'paints':{'Default1':{'baseColor':[.05,.05,.05,1],'metallic':.5,'roughness':.175},'Default2':{'baseColor':[1,.584314,0,1]}}}));mat=Materials(p,p,'lightweight',None,{},paint_color='#ff9500');self.assertEqual(mat.paints['Default1']['baseColor'],[1,149/255,0,1]);self.assertEqual(mat.paints['Default1']['metallic'],.5);self.assertEqual(mat.paints['Default2']['baseColor'],[1,.584314,0,1]);default=Materials(p,p,'lightweight',None,{});self.assertEqual(default.paints['Default1']['baseColor'],[.05,.05,.05,1])
 def test_actual_gtr_package(self):
  path=os.environ.get('KDA_GTR_PACKAGE')
  if not path:self.skipTest('Provide corrected GTR package')
  folder=Path(path)/'GameData/KDAVehicles/Vehicles/lizard_gtr';m=json.loads((folder/'vehicle.json').read_text());self.assertEqual(m['paint_color_override'],'#ff9500');self.assertTrue(all(x['enabled'] for x in m['drive']['active_hardware'].values()));self.assertAlmostEqual(m['drive']['rear_torque_fraction'],.58,places=5);body=(folder/'Parts/body.cfg').read_text();self.assertIn('importedRearVectoring = true',body);self.assertIn('activeCenterOffset = 0.95',body)
