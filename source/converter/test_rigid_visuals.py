import unittest
from kdaconvert.mesh import rigid_kind
from kdaconvert.characteristics import resolve

class RigidVisualTests(unittest.TestCase):
    def test_only_rigid_components_selected(self):
        for name,kind in [('06118_chassis1','chassis'),('06118_mainexhaustmesh255','exhaust'),('06118_engine0','engine')]:
            self.assertEqual(rigid_kind(name),kind)
        for name in ['06118_suspension_f4','06118_suspensionm_r15','06118_brakedisc_r12','06118_brakecaliper_f6','06118_tyre_f2','06118_wheel_f3']:
            self.assertIsNone(rigid_kind(name))
    def test_nested_defaults_and_disabled_slot(self):
        parts={'root':({'slots':[['type','default'],['exhaust','pipe'],['engine','motor']]},None),'pipe':({'slots':[['type','default'],['mesh','visual']]},None),'visual':({},None),'motor':({},None)}
        order,_=resolve(parts,{'parts':{'root':'root','engine':''}})
        self.assertEqual([name for name,_ in order],['root','pipe','visual'])

if __name__=='__main__':unittest.main()
