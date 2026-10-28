import copy,unittest
from kdaconvert.mass_distribution import reconstruct,ksp_centers

class MassDistributionTests(unittest.TestCase):
 def setUp(self):
  wheels=[dict(corner=c,axle=a,position_beamng_m=[x,y,.6],radius_m=.3) for c,a,x,y in [('FL','front',.7,-1),('FR','front',-.7,-1),('RL','rear',.7,1),('RR','rear',-.7,1)]]
  nodes=[['id','posX','posY','posZ'],{'nodeWeight':100}]
  for w in wheels:nodes.append([w['corner'],*w['position_beamng_m'],{'nodeWeight':300 if w['axle']=='front' else 100}])
  definitions=[['name','node1:','node2:']]+[[w['corner'],w['corner'],w['corner']] for w in wheels]
  self.pc={'parts':{'main':'root','Camso_wheels_front':'fw','Camso_wheels_rear':'rw'}}
  self.parts={'root':({'nodes':nodes,'pressureWheels':definitions},None)}
  for k in ['fw','rw']:self.parts[k]=({'pressureWheels':[['name','node1:','node2:'],{'numRays':4,'hubNodeWeight':2,'nodeWeight':3}]},None)
  self.m={'mass_kg':960,'wheels':wheels,'origin_beamng_m':[0,0,.6]}
 def test_weighted_nodes_and_generated_rings(self):
  d=reconstruct(self.m,self.parts,self.pc);self.assertTrue(d['valid'],d['issues']);self.assertEqual(d['generated_wheel_mass_kg'],160);self.assertEqual(d['reconstructed_mass_kg'],960)
  self.assertAlmostEqual(d['front_mass_fraction'],680/960);self.assertAlmostEqual(d['height_above_reference_tire_plane_m'],.3)
 def test_unselected_parts_excluded(self):
  self.parts['unselected']=({'nodes':[['id','posX','posY','posZ'],['bad',0,100,0,{'nodeWeight':100000}]]},None)
  self.assertTrue(reconstruct(self.m,self.parts,self.pc)['valid'])
 def test_solves_native_wheels_and_fuel_first_moment(self):
  self.m['mass_distribution']=reconstruct(self.m,self.parts,self.pc);target,body=ksp_centers(self.m)
  for i in range(3):
   moment=(960-44)*body[i]
   for w in self.m['wheels']:moment+=11*(-w['position_beamng_m'][i]+(.6 if i==2 else 0))
   self.assertAlmostEqual(moment/960,target[i])
  self.assertGreater(target[1],.4);self.assertAlmostEqual(target[2],0)
 def test_missing_weight_or_malformed_table_falls_back(self):
  self.parts['root'][0]['nodes'][1].clear();self.parts['root'][0]['nodes'][-1]=['bad']
  d=reconstruct(self.m,self.parts,self.pc);self.assertFalse(d['valid']);self.assertTrue(d['issues'])
 def test_large_mass_mismatch_falls_back(self):
  self.m['mass_kg']=2000;self.assertFalse(reconstruct(self.m,self.parts,self.pc)['valid'])
 def test_lua_not_executed(self):
  self.parts['fw'][0]['pressureWheels'][1]['nodeWeight']='$=os.execute("bad")'
  self.assertFalse(reconstruct(self.m,self.parts,self.pc)['valid'])
 def test_source_transform_falls_back(self):
  self.parts['root'][0]['nodeOffset']={'z':1};self.assertFalse(reconstruct(self.m,self.parts,self.pc)['valid'])
 def test_scale_and_lua_default(self):
  self.parts['root'][0]['scalenodeWeight']='$=($missing or 1)';self.assertTrue(reconstruct(self.m,self.parts,self.pc)['valid'])
 def test_duplicate_node_falls_back(self):
  self.parts['root'][0]['nodes'].append(copy.deepcopy(self.parts['root'][0]['nodes'][2]));self.assertFalse(reconstruct(self.m,self.parts,self.pc)['valid'])
 def test_whole_wheel_mass_overrides(self):
  for name in ['fw','rw']:self.parts[name][0]['pressureWheels'][1].update(hubWeight=16,tireWeight=24)
  self.assertTrue(reconstruct(self.m,self.parts,self.pc)['valid'])
 def test_unknown_wheel_weight_redistribution_is_labelled_fallback(self):
  self.parts['fw'][0]['pressureWheels'][1]['hubWeightGainRatio']=.2
  self.assertFalse(reconstruct(self.m,self.parts,self.pc)['valid'])
 def test_missing_axle_definition_is_labelled_fallback(self):
  self.parts['root'][0]['pressureWheels']=[]
  self.assertFalse(reconstruct(self.m,self.parts,self.pc)['valid'])
