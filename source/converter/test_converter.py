"""Generated-package integrity checks; no source scripts are executed."""
import json,sys,hashlib,zipfile,tempfile,unittest,math
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from kdaconvert.automation import relaxed_json,safe_extract
from kdaconvert.mesh import mu

def material_reader(self,m):
    self.name=m.read_string();self.shaderName=m.read_string()
    for _ in range(m.read_int()):
        name=m.read_string();kind=m.read_int()
        if kind==0:self.colorProperties[name]=m.read_float(4)
        elif kind==1:self.vectorProperties[name]=m.read_float(4)
        elif kind==2:self.floatProperties2[name]=m.read_float()
        elif kind==3:self.floatProperties3[name]=m.read_float()
        elif kind==4:self.textureProperties[name]=mu.MuMatTex().read(m)
        else:raise ValueError('Unknown material property')
    return self
mu.read_material4=material_reader

def inspect_model(path):
    model=mu.Mu().read(str(path));triangles=vertices=0
    def walk(o):
        nonlocal triangles,vertices
        mesh=getattr(o,'shared_mesh',None)
        if mesh:
            vertices+=len(mesh.verts)
            if len(mesh.verts)>65535:raise ValueError('16-bit vertex ceiling exceeded')
            if len(mesh.normals)!=len(mesh.verts) or len(mesh.uvs)!=len(mesh.verts):raise ValueError('Missing vertex attributes')
            if len(mesh.tangents)!=len(mesh.verts) or any(not all(math.isfinite(v) for v in t) for t in mesh.tangents):raise ValueError('Missing/invalid normal-map tangents')
            for sm in mesh.submeshes:
                triangles+=len(sm)
                if any(min(t)<0 or max(t)>=len(mesh.verts) for t in sm):raise ValueError('Invalid triangle index')
            if any(i>=len(model.materials) for i in o.renderer.materials):raise ValueError('Invalid renderer material')
        for c in o.children:walk(c)
    walk(model.obj)
    for tex in model.textures:
        if not (path.parent/(tex.name+'.png')).is_file():raise ValueError('Missing generated texture '+tex.name)
    return {'triangles':triangles,'vertices':vertices}

class ParserTests(unittest.TestCase):
 def test_comments_strings(self):self.assertEqual(relaxed_json('{"url":"http://test/*ok*/",/*x*/"x":[1,2,],}'),{'url':'http://test/*ok*/','x':[1,2]})
 def test_missing_commas(self):self.assertEqual(relaxed_json('{"a":1 "b":[1 2]}'),{'a':1,'b':[1,2]})
 def test_zip_traversal(self):
  with tempfile.TemporaryDirectory() as tmp:
   z=Path(tmp)/'bad.zip'
   with zipfile.ZipFile(z,'w') as f:f.writestr('../escape.txt','bad')
   with self.assertRaises(ValueError):safe_extract(z,Path(tmp)/'out')

if __name__=='__main__':
 if len(sys.argv)>1:
  root=Path(sys.argv[1]);vehicle=next((root/'GameData/KDAVehicles/Vehicles').iterdir());m=json.loads((vehicle/'vehicle.json').read_text())
  report={'models':{p.name:inspect_model(p) for p in (vehicle/'Models').glob('*.mu')},'engine_limit_rpm':m['engine']['limit_rpm'],'source_ratios':m['gearbox']['ratios'],'package_bytes':sum(p.stat().st_size for p in root.rglob('*') if p.is_file())}
  assert len(report['models'])==5 and report['engine_limit_rpm']==12000 and len(report['source_ratios'])==6
  assert abs(sum(m['mass_budget'][k] for k in ('body_dry_kg','wheels_kg','fuel_full_kg'))-m['mass_kg'])<1e-6
  # A ground-level export origin caused KSP to destroy the car on runway exit.
  # The converted root must be above every wheel's nominal ground plane.
  assert all(w['position_beamng_m'][2]-w['radius_m']<m['origin_beamng_m'][2] for w in m['wheels'])
  fixtures=m['visual'].get('fixtures',[])
  if fixtures:
   body=mu.Mu().read(str(vehicle/'Models/body.mu')).obj
   container=next(c for c in body.children if c.transform.name=='Fixtures')
   objects={c.transform.name:c for c in container.children}
   assert set(objects)=={f['id'] for f in fixtures},'Dropped or duplicated fixture objects'
   for fixture in fixtures:
    obj=objects[fixture['id']];pivot=obj.transform.localPosition
    expected=fixture['pivot_unity_m'];expected=(expected[0],expected[2],expected[1])
    assert max(abs(a-b) for a,b in zip(pivot,expected))<1e-5,'Incorrect fixture pivot'
    assert len(obj.children)==fixture['renderer_count'] and obj.children,'Empty fixture'
    assert not hasattr(obj,'collider'),'Cosmetic fixture must not add a collider'
   report['separate_fixtures']=len(fixtures)
  (root/'conversion-validation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
 else:unittest.main()
