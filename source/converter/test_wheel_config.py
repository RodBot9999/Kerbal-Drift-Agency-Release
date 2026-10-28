"""Functional regression checks against the previously tested wheel contract."""
import json
import math
from pathlib import Path
import re
import unittest
from unittest.mock import patch
from kdaconvert.wheel_config import wheel_part


def read_config(text):
    root={'name':'ROOT','fields':{},'children':[]};stack=[root];pending=None
    for raw in text.splitlines():
        line=raw.split('//',1)[0].strip()
        if not line:continue
        if line=='{':
            node={'name':pending,'fields':{},'children':[]};stack[-1]['children'].append(node);stack.append(node);pending=None
        elif line=='}':stack.pop()
        elif '=' in line:
            k,v=line.split('=',1);stack[-1]['fields'].setdefault(k.strip(),[]).append(v.strip())
        else:pending=line
    if len(stack)!=1:raise ValueError('Unbalanced configuration')
    return root['children'][0]


def equivalent(value):
    value=re.sub(r'\s+','',value).lower()
    try:return tuple(float(x) for x in value.split(','))
    except ValueError:return value


class WheelConfigTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixtures=json.loads((Path(__file__).parent/'testdata/wheel-compatibility.json').read_text())

    def test_calibration_geometry_and_module_order_match_four_cars(self):
        for item in self.fixtures:
            with self.subTest(car=item['vehicle']['id'],corner=item['wheel']['corner']):
                part=read_config(wheel_part(item['vehicle'],item['wheel']))
                for k,v in item['part_fields'].items():self.assertEqual(equivalent(part['fields'][k][0]),equivalent(v),k)
                modules=[n for n in part['children'] if n['name']=='MODULE']
                self.assertEqual([n['fields']['name'][0] for n in modules],list(item['modules']))
                for node in modules:
                    name=node['fields']['name'][0]
                    for k,v in item['modules'][name].items():self.assertEqual(equivalent(node['fields'][k][0]),equivalent(v),name+'/'+k)
                model=next(n for n in part['children'] if n['name']=='MODEL')
                self.assertEqual(model['fields']['model'][0],item['model'])

    def test_no_template_access_or_legacy_response_curve(self):
        item=self.fixtures[0]
        with patch.object(Path,'read_text',side_effect=AssertionError('Wheel generation must not load a template')):
            part=read_config(wheel_part(item['vehicle'],item['wheel']))
        modules=part['children']
        self.assertFalse(any(n['name']=='MODULE' and n['fields']['name'][0]=='ModuleWheelDamage' for n in modules))
        steering=next(n for n in modules if n['name']=='MODULE' and n['fields']['name'][0]=='ModuleWheelSteering')
        self.assertFalse(steering['children']);self.assertEqual(steering['fields']['autoSteeringAdjust'],['false'])
        motor=next(n for n in modules if n['name']=='MODULE' and n['fields']['name'][0]=='KDAWheelMotor')
        curve=motor['children'][0]['fields']['key']
        self.assertEqual(curve,['0 2 0 0','150 2 0 0'])

    def test_front_rear_drive_rules_and_normalized_awd(self):
        base=self.fixtures[0]
        for drive in ['FWD','RWD','AWD']:
            total=0
            for corner in ['FL','FR','RL','RR']:
                front=corner.startswith('F');driven=drive=='AWD' or front==(drive=='FWD')
                weight=(.2 if front else .3) if drive=='AWD' else .5 if driven else 0
                v={**base['vehicle'],'drivetrain':drive};w={**base['wheel'],'corner':corner,'axle':'front' if front else 'rear','drive_weight':weight}
                part=read_config(wheel_part(v,w));motor=next(n for n in part['children'] if n['name']=='MODULE' and n['fields']['name'][0]=='KDAWheelMotor')
                self.assertEqual(motor['fields']['motorEnabled'][0],str(driven).lower());total+=float(motor['fields']['driveWeight'][0])
            self.assertAlmostEqual(total,1)

    def test_invalid_data_rejected_and_pbr_nodes_preserved(self):
        item=self.fixtures[0]
        for field,value in [('radius_m',0),('radius_m',math.nan),('grip_calibration',-1),('drive_ratio',0),('drive_weight',2)]:
            with self.subTest(field=field,value=value),self.assertRaises(ValueError):wheel_part(item['vehicle'],{**item['wheel'],field:value})
        with self.assertRaises(ValueError):wheel_part({**item['vehicle'],'name':'bad\nMODULE'},item['wheel'])
        text=wheel_part(item['vehicle'],item['wheel'],'SHABBY_MATERIAL_REPLACE\n{\nmaterialDef = test\n}\n')
        self.assertEqual(read_config(text)['children'][-1]['name'],'SHABBY_MATERIAL_REPLACE')


if __name__=='__main__':unittest.main()
