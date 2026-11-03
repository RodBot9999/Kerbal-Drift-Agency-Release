import unittest
from kdaconvert.suspension_visual import anchors
from kdaconvert.mesh import suspension_corner
import numpy as np

class SuspensionVisualTests(unittest.TestCase):
    def test_trim_offsets_and_selected_layout(self):
        parts={
          'trim':({'slots':[['type','default'],['Camso_suspension_F','front',{'unused':0},{'nodeOffset':{'x':'.02','y':'$=-1.2 + ($offset or 0)','z':.7}}]]},None),
          'front':({'slotType':'Camso_suspension_F','variables':[['name','type','unit','category','default'],['$offset','range','m','test',.1]],'nodes':[['id','posX','posY','posZ'],{'group':['hub_F','susp_F']},['hubL',.7,0,0],['hubR',-.7,0,0],{'group':['subframe_F','susp_F']},['mountL',.3,0,.2],['mountR',-.3,0,.2]]},None),
        }
        result=anchors(parts,{'parts':{'Camso_suspension_F':'front','Camso_Trim':'trim'}})
        self.assertEqual(set(result),{'FL','FR'})
        self.assertAlmostEqual(result['FL']['moving'][0][0],.72)
        self.assertAlmostEqual(result['FL']['moving'][0][1],-1.1)
        self.assertAlmostEqual(result['FR']['fixed'][0][2],.9)
    def test_missing_attachment_groups_skip_visual_rig(self):
        parts={'trim':({'slots':[['type','default'],['Camso_suspension_R','rear']]},None),'rear':({'slotType':'Camso_suspension_R','nodes':[['id','posX','posY','posZ'],{'group':'susp_R'},['n',.5,0,0]]},None)}
        self.assertEqual(anchors(parts,{'parts':{'Camso_Trim':'trim'}}),{})
    def test_live_rear_axle_uses_rigid_fallback_instead_of_front_rig(self):
        wheels=[{'corner':c,'axle':a,'position_beamng_m':[x,0,0]} for c,a,x in [('FL','front',1),('FR','front',-1),('RL','rear',1),('RR','rear',-1)]]
        front={'FL':{'fixed':[[0,0,0]],'moving':[[1,0,0]]},'FR':{'fixed':[[0,0,0]],'moving':[[-1,0,0]]}}
        self.assertIsNone(suspension_corner({'node_groups':['susp_R']},wheels,front,np.array([[-1.,0,0],[1.,0,0]])))
        self.assertEqual(suspension_corner({'node_groups':['susp_F']},wheels,front,np.array([[.8,0,0],[1.,0,0]])),'FL')
    def test_unknown_attachment_layout_is_preserved_without_guessing_animation(self):
        self.assertIsNone(suspension_corner({'node_groups':['custom']},[],{},np.array([[0.,0,0]])))

if __name__=='__main__':unittest.main()
