from fixture_paths import export_fixture
import unittest
from kdaconvert.engine_metadata import engine_tables
from kdaconvert.automation import inspect

class EngineMetadataTests(unittest.TestCase):
 def test_no_execution_and_nested_values(self):
  tables=engine_tables('do local _={Family={BlockConfig="EngBlock_Inl4_Name",Bore=80},Variant={Nested={Capacity=99},Capacity=2.0,Bore=86,Stroke=86,Name="test } text",RPMLimit=os.execute("bad")}} return _')
  self.assertEqual(tables['Variant']['Capacity'],2)
  self.assertEqual(tables['Variant']['Name'],'test } text')
  self.assertNotIn('RPMLimit',tables['Variant'])
 def test_exported_corvette(self):
  e=inspect(export_fixture('analysis-corvette'),'test')[0]['engine']
  self.assertEqual(e['layout'],'V8');self.assertAlmostEqual(e['displacement_l'],6.65012332152)
  self.assertEqual(e['bore_mm'],105);self.assertEqual(e['stroke_mm'],96)
  self.assertEqual(e['limit_rpm'],10400)
 def test_exported_leopard(self):
  m=inspect(export_fixture('analysis-leopard'),'test')[0];e=m['engine']
  self.assertEqual(e['layout'],'I4');self.assertAlmostEqual(e['displacement_l'],2.3040063503982)
  self.assertIn('Embedded Automation',e['layout_basis'])
  self.assertFalse(any('displacement is not present' in w for w in m['warnings']))

if __name__=='__main__':unittest.main()
