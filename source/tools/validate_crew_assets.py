import sys,json,math,collections
import numpy as np
from pathlib import Path
root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'tools/vendor/io_object_mu'));import mu
def read_material(self,m):
    self.name=m.read_string();self.shaderName=m.read_string()
    for _ in range(m.read_int()):
        name=m.read_string();kind=m.read_int()
        if kind==0:self.colorProperties[name]=m.read_float(4)
        elif kind==1:self.vectorProperties[name]=m.read_float(4)
        elif kind==2:self.floatProperties2[name]=m.read_float()
        elif kind==3:self.floatProperties3[name]=m.read_float()
        elif kind==4:self.textureProperties[name]=mu.MuMatTex().read(m)
    return self
mu.read_material4=read_material
models=root.parent/'GameData/KDAVehicles/Crew/Models';report={}
reports=root/'.build/validation';reports.mkdir(parents=True,exist_ok=True)
for name in ['race-seat','race-seat-internal','door-marker']:
    m=mu.Mu().read(str(models/(name+'.mu')));objects=[];used=set();triangles=0
    def visit(o):
        global triangles
        objects.append(o)
        if hasattr(o,'shared_mesh'):
            mesh=o.shared_mesh;assert len(mesh.verts)==len(mesh.normals)==len(mesh.uvs)
            assert all(math.isfinite(x) for v in mesh.verts for x in v)
            assert all(0<=i<len(mesh.verts) for t in mesh.submeshes[0] for i in t)
            v=np.array(mesh.verts);n=np.array(mesh.normals);edges=collections.Counter()
            assert np.isfinite(n).all() and np.allclose(np.linalg.norm(n,axis=1),1,atol=1e-4)
            uv=np.asarray(mesh.uvs);assert np.isfinite(uv).all()
            for triangle in mesh.submeshes[0]:
                face=v[list(triangle)];normal=np.cross(face[1]-face[0],face[2]-face[0])
                assert np.dot(normal,n[list(triangle)].mean(axis=0))>1e-12,'Face winding opposes surface normals'
                t=uv[list(triangle)];a=t[1]-t[0];b=t[2]-t[0]
                assert abs(a[0]*b[1]-a[1]*b[0])>1e-12,'Collapsed UV triangle'
                points=[tuple(np.round(point,6)) for point in face]
                for a,b in zip(points,points[1:]+points[:1]):edges[tuple(sorted((a,b)))]+=1
            assert all(count==2 for count in edges.values()),'Seat volume has open or nonmanifold edges'
            used.update(o.renderer.materials);triangles+=len(mesh.submeshes[0])
        for c in o.children:visit(c)
    visit(m.obj);assert used==set(range(len(m.materials))),'Unused material table entries cause native PartReader failure'
    for texture in m.textures:assert (models/(texture.name+'.png')).is_file()
    colliders=[o for o in objects if hasattr(o,'collider')]
    if name=='race-seat':
        airlock=next(o for o in objects if o.transform.name=='KDAAirlock');assert airlock.tag_and_layer.tag=='Airlock' and airlock.tag_and_layer.layer==21 and airlock.collider.isTrigger
    elif name=='race-seat-internal':
        assert not colliders;assert any(o.transform.name=='seatPilot' for o in objects)
        if (root/'assets/seat-recaro/master.obj').exists():
            pilot=next(o for o in objects if o.transform.name=='seatPilot')
            assert np.allclose(pilot.transform.localPosition,(0,-.045,-.035),atol=1e-5),'Wrong authored IVA occupant offset'
        assert all(o.tag_and_layer.layer==16 for o in objects)
        assert any(hasattr(o,'light') and o.light.cullingMask==1<<16 for o in objects)
    report[name]={'triangles':triangles,'materials':len(m.materials),'colliders':len(colliders),'closed_volumes':True,'winding_matches_normals':True,'validated':True}
seat=(models.parent/'seat.cfg').read_text();assert 'CrewCapacity = 1' in seat and 'name = InternalSeat' in seat and 'KerbalSeat' not in seat
assert 'name = ModuleCommand' in seat and 'minimumCrew = 1' in seat
patch=(models.parent/'imported-crew.cfg').read_text();assert '@PART[kda-*-body]' in patch and '@CrewCapacity = 0' in patch and '!MODULE[ModuleCommand],*' in patch and 'name = KDAImportedCrew' in patch
(reports/'crew-assets.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
