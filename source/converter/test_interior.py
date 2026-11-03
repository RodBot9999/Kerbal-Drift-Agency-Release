import copy,unittest
import numpy as np
from kdaconvert.mesh import mu,node
from kdaconvert.interior import components,rig,subset
from kdaconvert.lighting import isolate_rear,definitions


def ring(center=(-.35,-.5,.1),radius=.17):
    obj=node('example_body_rim');mesh=mu.MuMesh();vertices=[];tri=[]
    for i in range(96):
        angle=i/96*2*np.pi
        for r in (radius-.01,radius+.01):vertices.append((center[0]+r*np.cos(angle),center[1]+r*np.sin(angle),center[2]))
    for i in range(96):
        a=i*2;b=((i+1)%96)*2;tri.extend([(a,b,b+1),(a,b+1,a+1)])
    mesh.verts=vertices;mesh.normals=[(0,0,1)]*len(vertices);mesh.uvs=[(0,0)]*len(vertices);mesh.submeshes=[tri];obj.shared_mesh=mesh;obj.renderer=mu.MuRenderer();obj.renderer.materials=[0];return obj


def fixture():
    model=mu.Mu('body');model.obj=node('body');model.materials=[]
    manifest={'warnings':[],'wheels':[dict(axle='front',position_beamng_m=[x,-1.1,.5]) for x in (-.7,.7)]+[dict(axle='rear',position_beamng_m=[x,1.1,.5]) for x in (-.7,.7)],'lighting':{'materials':[]}}
    return model,manifest


class InteriorTests(unittest.TestCase):
    def test_rim_pivot_axis_and_world_vertices_preserved(self):
        model,m=fixture();obj=ring();model.obj.children=[obj];before=np.asarray(obj.shared_mesh.verts)
        data=rig(model,m);self.assertTrue(data['steering_rigged']);self.assertEqual(data['triangles'],192)
        wheel=model.obj.children[0];after=np.asarray(wheel.children[0].shared_mesh.verts)+np.asarray(wheel.transform.localPosition)
        np.testing.assert_allclose(before,after,atol=1e-12);np.testing.assert_allclose(data['axis_body_local'],[0,-1,0],atol=1e-10)
        np.testing.assert_allclose(data['pivot_body_local_m'],[-.35,.1,-.5],atol=1e-10)
    def test_ambiguous_two_separate_wheels_remain_static(self):
        model,m=fixture();model.obj.children=[ring(),ring(center=(.35,-.5,.1))]
        data=rig(model,m);self.assertFalse(data['steering_rigged']);self.assertEqual(len(model.obj.children),2);self.assertTrue(m['warnings'])
    def test_small_dashboard_knob_not_mistaken_for_steering(self):
        model,m=fixture();model.obj.children=[ring(radius=.04)];self.assertFalse(rig(model,m)['steering_rigged'])
    def test_welded_uv_seams_share_connected_component(self):
        obj=ring();mesh=obj.shared_mesh;v=mesh.verts;t=mesh.submeshes[0];mesh.verts=[v[i] for triangle in t for i in triangle];mesh.submeshes=[[(i,i+1,i+2) for i in range(0,len(mesh.verts),3)]]
        self.assertEqual(len(components(mesh)),1)
    def test_subset_retains_uv_normals_and_does_not_mutate_source(self):
        mesh=ring().shared_mesh;original=copy.deepcopy(mesh);mask=np.arange(192)%2==0;part=subset(mesh,mask)
        self.assertEqual(mesh.verts,original.verts);self.assertEqual(len(part.verts),len(part.normals));self.assertEqual(len(part.verts),len(part.uvs));self.assertEqual(len(part.submeshes[0]),96)
    def test_shared_front_rear_lens_split_without_geometry_loss(self):
        model,m=fixture();obj=ring(center=(0,-.5,-2));front=ring(center=(0,-.5,2));offset=len(obj.shared_mesh.verts)
        obj.shared_mesh.verts+=front.shared_mesh.verts;obj.shared_mesh.normals+=front.shared_mesh.normals;obj.shared_mesh.uvs+=front.shared_mesh.uvs;obj.shared_mesh.submeshes[0]+=[tuple(i+offset for i in t) for t in front.shared_mesh.submeshes[0]]
        model.obj.children=[obj];mat=mu.MuMaterial();mat.name='shared_lens';model.materials=[mat];m['lighting']['materials']=[dict(material='shared_lens',functions={'lowbeam':1})]
        isolate_rear(model,m);self.assertEqual(len(model.obj.children),2);self.assertEqual(sum(len(x.shared_mesh.submeshes[0]) for x in model.obj.children),384)
        rear=next(x for x in model.obj.children if x.transform.name.startswith('KDARearBrakeSkin_'));self.assertTrue(all(v[2]<-1.1 for v in rear.shared_mesh.verts))
        m.update(id='test',origin_beamng_m=[0,0,.5],runtime={'body_part':'kda-test-body'});self.assertIn('rearLampPlane = -1.1',definitions(m))
    def test_indicator_lenses_keep_original_function(self):
        model,m=fixture();obj=ring(center=(0,-.5,-2));model.obj.children=[obj];mat=mu.MuMaterial();mat.name='indicator';model.materials=[mat];m['lighting']['materials']=[dict(material='indicator',functions={'lowbeam':1,'signal_R':1})]
        isolate_rear(model,m);self.assertFalse(m['lighting']['rear_brake_groups']);self.assertEqual(obj.transform.name,'example_body_rim')

if __name__=='__main__':unittest.main()
