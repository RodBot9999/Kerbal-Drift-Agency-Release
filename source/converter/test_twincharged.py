"""Regression fixture: selected numeric data from the user's Rabbit Rally X export.

The adjacent source notice covers the extracted data. No meshes or audio are
needed to test torque reconstruction, induction selection and drivetrain data.
"""
import copy,json,unittest
from pathlib import Path
from kdaconvert.characteristics import enrich,induction_type
from kdaconvert import ksp  # The GUI also imports this writer before conversion.

class TwinChargedTests(unittest.TestCase):
    def setUp(self):
        self.fixture=json.loads((Path(__file__).parent/'testdata/twincharged-physics.json').read_text())
    def parse(self,kind=None,remove=None):
        data=copy.deepcopy(self.fixture)
        if kind:data['meta']['Induction Type']=kind
        parts={k:(v,Path('fixture.jbeam')) for k,v in data['parts'].items()}
        if remove:
            for key in list(parts):
                if remove in parts[key][0]:del parts[key]
        enrich(data['seed'],parts,data['pc'],data['meta'])
        return data['seed']
    def test_real_twincharged_engine_and_awd(self):
        m=self.parse();e=m['engine']
        self.assertEqual(e['induction'],'TwinCharged')
        self.assertEqual(e['limit_rpm'],8000)
        self.assertEqual(e['supercharger_disengage_rpm'],3400)
        self.assertAlmostEqual(e['mechanical_boost_max_bar'],2.628970954677)
        self.assertAlmostEqual(e['max_boost_bar'],2.7)
        self.assertAlmostEqual(e['peak_torque_nm'],606,delta=1)
        self.assertEqual(m['gearbox']['type'],'sequential')
        self.assertEqual(len(m['gearbox']['ratios']),6)
        self.assertEqual(m['drive']['rear_torque_fraction'],.5)
        self.assertEqual(m['drive']['front_torque_fraction'],.5)
    def test_combined_pressure_reconstructs_source_torque_once(self):
        e=self.parse()['engine'];p=dict(e['boost_curve_bar']);net=dict(e['full_load_torque_curve'])
        for rpm,torque in e['torque_curve']:
            self.assertAlmostEqual(torque*(1+p[rpm]),net[rpm],places=8)
    def test_supercharged_pressure_is_not_replaced_by_generic_preset(self):
        e=self.parse('SuperCharged',remove='turbocharger')['engine']
        self.assertEqual(e['induction'],'Supercharged')
        self.assertEqual(e['spool_seconds'],.08)
        self.assertAlmostEqual(e['max_boost_bar'],2.7)
    def test_twincharged_requires_both_selected_devices(self):
        with self.assertRaisesRegex(ValueError,'both selected'):
            self.parse(remove='supercharger')
    def test_unrecognized_induction_still_rejected(self):
        with self.assertRaisesRegex(ValueError,'Unsupported induction'):
            induction_type('Electric boost magic')
        self.assertEqual(induction_type('Twin Charged'),'TwinCharged')
        self.assertEqual(induction_type('NA'),'NA')
        self.assertEqual(induction_type('Super'),'Supercharged')

if __name__=='__main__':unittest.main()
