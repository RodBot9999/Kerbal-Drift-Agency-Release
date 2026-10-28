from fixture_paths import export_fixture
import unittest,tempfile,json
from pathlib import Path
from kdaconvert.exhaust_visual import outlets
from kdaconvert.automation import inspect
from kdaconvert.materials import Materials
from kdaconvert.mesh import mu

class ExportNotesTests(unittest.TestCase):
    def test_dual_outlets_exclude_engine_inlet(self):
        p={'slotType':'Camso_exhaust','nodes':[['id','x','y','z'],['in',0,0,0],['mid',0,1,0],['left',1,2,0],['right',-1,2,0]],'beams':[['a','b'],['engine','in',{'isExhaust':'mainEngine'}],['in','mid',{'isExhaust':'mainEngine'}],['mid','left',{'isExhaust':'mainEngine'}],['mid','right',{'isExhaust':'mainEngine'}]]}
        tips=outlets({'pipe':(p,None)},{'parts':{'exhaust':'pipe'}})
        self.assertEqual({r['node'] for r in tips},{'left','right'})
        self.assertGreater(tips[0]['direction_beamng'][1],0)
    def test_source_corvette_tips(self):
        m,_=inspect(export_fixture('analysis-corvette'),'test')
        self.assertEqual(len(m['exhaust_outlets']),2)
        self.assertGreater(m['engine']['peak_torque_nm'],700)
    def test_cast_hardware_finish_does_not_change_paint(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);(root/'info.json').write_text(json.dumps({'paints':{'Default1':{'baseColor':[1,1,1,1]}}}))
            (root/'test.materials.json').write_text(json.dumps({'cast':{'Stages':[{'roughnessFactor':.05,'metallicFactor':1}]},'body':{'Stages':[{'roughnessFactor':.1,'metallicFactor':.5}]}}))
            mats=Materials(root,root,'balanced',mu,{'123_engine0':[{'material':'cast'}],'123_body0':[{'material':'body'}]});mats.get('cast');mats.get('body')
            self.assertEqual(mats.report[0]['roughness'],.6)
            self.assertEqual(mats.report[1]['roughness'],.1)

if __name__=='__main__':unittest.main()
