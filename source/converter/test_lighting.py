import unittest,json,tempfile
from pathlib import Path
import numpy as np
from kdaconvert.lighting import extract,emission_image,definitions
class LightingTests(unittest.TestCase):
 def fixture(self,root):
  materials={'lamp_on':{'Stages':[{'emissiveFactor':[1,.1,.02]}]}}
  (root/'main.materials.json').write_text(json.dumps(materials))
  part={'nodes':[['id','posX','posY','posZ'],['a',0,-2,1],['b',-.1,-2,1],['c',0,-2,1.1]],'glowMap':{'lamp':{'simpleFunction':{'lowbeam':.49,'highbeam':1},'on':'lamp_on'}},'props':[['func','mesh','idRef:','idX:','idY:','baseRotation','rotation','translation','min','max','offset','multiplier'],{'lightOuterAngle':60,'lightRange':50,'lightColor':{'r':255,'g':128,'b':0}},['lowbeam','SPOTLIGHT','a','b','c',{'x':0,'y':0,'z':0},{},{},0,0,0,1,{'lightBrightness':.3}]]}
  return {'selected':(part,root/'main.jbeam'),'unselected':({'props':part['props']},root/'other.jbeam')},{'parts':{'Camso_Trim':'selected'}}
 def test_selected_source_and_headlight_direction(self):
  with tempfile.TemporaryDirectory() as d:
   parts,pc=self.fixture(Path(d));data=extract(parts,pc,Path(d));self.assertEqual(len(data['emitters']),1);lamp=data['emitters'][0];np.testing.assert_allclose(lamp['direction_beamng'],[0,-1,0],atol=1e-8);self.assertEqual(lamp['range_m'],50);self.assertAlmostEqual(lamp['color_rgb'][1],128/255);self.assertEqual(data['materials'][0]['emissive_rgb'],[1,.1,.02])
 def test_degenerate_reference_is_skipped(self):
  with tempfile.TemporaryDirectory() as d:
   parts,pc=self.fixture(Path(d));parts['selected'][0]['nodes'][2][1:]=[0,-2,1];data=extract(parts,pc,Path(d));self.assertEqual(data['emitters'],[]);self.assertTrue(data['warnings'])
 def test_missing_dynamic_material_is_not_invented(self):
  with tempfile.TemporaryDirectory() as d:
   parts,pc=self.fixture(Path(d));parts['selected'][0]['glowMap']['lamp']['on']='missing_dynamic_dash';data=extract(parts,pc,Path(d));self.assertFalse(data['materials']);self.assertTrue(data['warnings'])
 def test_no_lighting_export_is_valid(self):
  with tempfile.TemporaryDirectory() as d:
   self.assertEqual(extract({}, {'parts':{}},Path(d))['emitters'],[])
 def test_unsupported_function_is_not_activated(self):
  with tempfile.TemporaryDirectory() as d:
   parts,pc=self.fixture(Path(d));parts['selected'][0]['props'][-1][0]='police_magic';self.assertEqual(extract(parts,pc,Path(d))['emitters'],[])
 def test_light_properties_do_not_leak_between_rows(self):
  with tempfile.TemporaryDirectory() as d:
   parts,pc=self.fixture(Path(d));p=parts['selected'][0]['props'];row=list(p[-1]);p[-1][-1]={'lightRange':80};row[-1]={'lightBrightness':.4};p.append(row);self.assertEqual([x['range_m'] for x in extract(parts,pc,Path(d))['emitters']],[80,50])
 def test_unlit_lens_color_does_not_suppress_emission(self):
  rgba=np.array([[[.01,.02,.03,.08],[.01,.02,.03,0]]],dtype=np.float32);image=emission_image(rgba,True,.08);np.testing.assert_allclose(image[0,0],[1,1,1,1]);np.testing.assert_allclose(image[0,1],[1,1,1,0]);np.testing.assert_allclose(rgba[0,0],[.01,.02,.03,.08])
 def test_body_coordinate_mapping_and_signal_roundtrip(self):
  with tempfile.TemporaryDirectory() as d:
   parts,pc=self.fixture(Path(d));m={'id':'test','runtime':{'body_part':'kda-test-body'},'origin_beamng_m':[0,0,.4],'lighting':extract(parts,pc,Path(d))};m['lighting']['materials'][0]['texture']='lamp_emission';text=definitions(m);self.assertIn('position = 0,2,-0.6',text);self.assertIn('direction = 0,1,0',text);self.assertIn('signal = lowbeam,0.49',text);self.assertIn('Models/lamp_emission',text)
if __name__=='__main__':unittest.main()
