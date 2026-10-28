"""COLLADA visual geometry to KSP .mu. No Blender installation required."""
from pathlib import Path
import sys,math,xml.etree.ElementTree as ET,json
import numpy as np
from PIL import Image
from .automation import relaxed_json
from .materials import Materials
ROOT=Path(sys._MEIPASS) if getattr(sys,'frozen',False) else Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/vendor/io_object_mu'))
import mu
NS={'c':'http://www.collada.org/2005/11/COLLADASchema'}

def rigid_kind(mesh):
    name=mesh.lower()
    if '_chassis' in name:return 'chassis'
    if '_mainexhaustmesh' in name or '_exhaust' in name:return 'exhaust'
    if '_engine' in name:return 'engine'
    return None

def suspension_corner(binding, wheels, anchors, points):
    """Choose a supported independent rig; retain other layouts as rigid art."""
    groups=binding.get('node_groups',[])
    axle='front' if 'susp_F' in groups else 'rear' if 'susp_R' in groups else None
    candidates=[w for w in wheels if w['axle']==axle]
    if not candidates:return None
    wheel=min(candidates,key=lambda w:abs(w['position_beamng_m'][0]-float(points[:,0].mean())))
    corner=wheel['corner']
    return corner if anchors.get(corner) else None

def node(name):
    o=mu.MuObject(name);o.transform=mu.MuTransform();o.transform.name=name;o.transform.localPosition=(0,0,0);o.transform.localRotation=(1,0,0,0);o.transform.localScale=(1,1,1)
    o.tag_and_layer=mu.MuTagLayer();o.tag_and_layer.tag='Untagged';o.tag_and_layer.layer=0;return o

def rotation(deg):
    x,y,z=np.radians(deg);cx,sx=math.cos(x),math.sin(x);cy,sy=math.cos(y),math.sin(y);cz,sz=math.cos(z),math.sin(z)
    return np.array([[cz,-sz,0],[sz,cz,0],[0,0,1]])@np.array([[cy,0,sy],[0,1,0],[-sy,0,cy]])@np.array([[1,0,0],[0,cx,-sx],[0,sx,cx]])

def beam_rotation(deg):
    # Flexbody placements use ZXY Euler, not the XYZ used by our rig helpers.
    # https://documentation.beamng.com/modding/vehicle/intro_jbeam/jbeamtips/
    x,y,z=deg
    return rotation([0,y,0])@rotation([x,0,0])@rotation([0,0,z])

def wheel_rotation(deg):
    # Tire/rim geometry is authored about its local X axle. Camber and toe
    # baked into rotating vertices orbit around KSP's straight spin axis.
    # Keep axle roll and side mirroring; alignment belongs to the part rig.
    x,y,z=deg
    return beam_rotation([x,0,round(z/180)*180])

def unity_to_mu(p):return p[:,[0,2,1]]
def beam_to_unity(p):return -p

def tangents(vertices,normals,uv,triangles):
    a,b,c=triangles.T;edge1=vertices[b]-vertices[a];edge2=vertices[c]-vertices[a]
    d1=uv[b]-uv[a];d2=uv[c]-uv[a];det=d1[:,0]*d2[:,1]-d1[:,1]*d2[:,0]
    inv=np.divide(1,det,out=np.zeros_like(det),where=np.abs(det)>1e-12)
    s=(edge1*d2[:,1,None]-edge2*d1[:,1,None])*inv[:,None]
    t=(edge2*d1[:,0,None]-edge1*d2[:,0,None])*inv[:,None]
    ss=np.zeros_like(vertices);tt=np.zeros_like(vertices)
    for index in (a,b,c):np.add.at(ss,index,s);np.add.at(tt,index,t)
    ss-=normals*np.sum(normals*ss,axis=1)[:,None]
    lengths=np.linalg.norm(ss,axis=1);missing=lengths<1e-9
    if np.any(missing):
        reference=np.tile([1.,0,0],(np.sum(missing),1));reference[np.abs(normals[missing,0])>.9]=[0,1.,0]
        ss[missing]=np.cross(normals[missing],reference);lengths=np.linalg.norm(ss,axis=1)
    ss/=np.maximum(lengths,1e-9)[:,None]
    handed=np.where(np.sum(np.cross(normals,ss)*tt,axis=1)<0,-1.,1.)
    # The MU writer reverses tangent handedness when converting from its RHS.
    return np.column_stack((unity_to_mu(ss),-handed))

def geometries(vehicle):
    result={}
    for path in vehicle.rglob('*.dae'):
        doc=ET.parse(path);unit=float(doc.find('.//c:unit',NS).get('meter','1'));axis=doc.findtext('.//c:up_axis',namespaces=NS)
        if axis!='Z_UP':raise ValueError('First COLLADA adapter requires Z_UP')
        for geo in doc.findall('.//c:geometry',NS):
            name=geo.get('id').removesuffix('-lib');mesh=geo.find('c:mesh',NS);sources={};vertices={}
            for src in mesh.findall('c:source',NS):
                array=src.find('c:float_array',NS);acc=src.find('.//c:accessor',NS)
                sources[src.get('id')]=np.fromstring(array.text,sep=' ').reshape(-1,int(acc.get('stride','1')))
            for verts in mesh.findall('c:vertices',NS):vertices[verts.get('id')]=verts.find('c:input',NS).get('source')[1:]
            groups=[]
            for tri in mesh.findall('c:triangles',NS):
                inputs=tri.findall('c:input',NS);stride=max(int(i.get('offset')) for i in inputs)+1
                indices=np.fromstring(tri.findtext('c:p',namespaces=NS),sep=' ',dtype=np.int64).reshape(-1,stride)
                data={}
                texcoords=[i for i in inputs if i.get('semantic')=='TEXCOORD']
                duplicate_uv_sets=len({i.get('set','0') for i in texcoords})!=len(texcoords)
                for i in inputs:
                    semantic=i.get('semantic');source=i.get('source')[1:]
                    if semantic=='VERTEX':semantic='POSITION';source=vertices[source]
                    if semantic=='TEXCOORD':
                        uvset=i.get('set','0')
                        # Automation writes both inputs with set="0", although
                        # their source IDs explicitly end in -UV0 and -UV1.
                        # Do not overwrite the tiled detail UV with the stamp UV.
                        if duplicate_uv_sets:
                            suffix=source.rsplit('-UV',1)
                            if len(suffix)!=2 or not suffix[1].isdigit():
                                raise ValueError('Ambiguous duplicate COLLADA texture sets: '+source)
                            uvset=suffix[1]
                        semantic='TEXCOORD'+uvset
                    data[semantic]=sources[source][indices[:,int(i.get('offset'))]]
                positions=data['POSITION'][:,:3]*unit;normal=data.get('NORMAL',np.zeros_like(positions))[:,:3];uv=np.nan_to_num(data.get('TEXCOORD0',np.zeros((len(positions),2)))[:,:2],nan=0,posinf=0,neginf=0)
                groups.append({'material':tri.get('material'),'positions':positions,'normals':normal,'uv':uv,'uv0':uv,'uv1':np.nan_to_num(data.get('TEXCOORD1',uv)[:,:2],nan=0,posinf=0,neginf=0)})
            result[name]=groups
    return result

def add_groups(parent,groups,transform,materials,label):
    count=0
    for group in groups:
        verts,normals=transform(group['positions'],group['normals']);material,uvset=materials.get(group['material']);uv=group['uv'+str(uvset)]
        # Deduplicate exact position/normal/UV tuples; split before the legacy
        # Unity 16-bit mesh index ceiling. No arbitrary triangle deletion.
        for start in range(0,len(verts),60000):
            v=verts[start:start+60000];n=normals[start:start+60000];u=uv[start:start+60000]
            packed=np.column_stack((v,n,u));unique,inverse=np.unique(packed,axis=0,return_inverse=True)
            triangles=inverse.reshape(-1,3);valid=(triangles[:,0]!=triangles[:,1])&(triangles[:,1]!=triangles[:,2])&(triangles[:,0]!=triangles[:,2]);triangles=triangles[valid]
            if not len(triangles):continue
            o=node(label+'_'+str(count));mesh=mu.MuMesh();mesh.verts=list(map(tuple,unity_to_mu(unique[:,:3])));mesh.normals=list(map(tuple,unity_to_mu(unique[:,3:6])));mesh.uvs=list(map(tuple,unique[:,6:8]));mesh.submeshes=[list(map(tuple,triangles.tolist()))];o.shared_mesh=mesh;o.renderer=mu.MuRenderer();o.renderer.materials=[material];parent.children.append(o);count+=1
            mesh.tangents=list(map(tuple,tangents(unique[:,:3],unique[:,3:6],unique[:,6:8],triangles)))
    return count

def export_models(manifest,vehicle,output,quality):
    output.mkdir(parents=True,exist_ok=True);geos=geometries(vehicle);materials=Materials(vehicle,output,quality,mu,geos,manifest.get('lighting'),manifest.get('paint_color_override'));body=node('body');models={};visual=[];triangles=0
    # Rigid hardware and the optional suspension skin are cosmetic only.
    bindings=manifest['visual_bindings'];shell=[b for b in bindings if '_body' in b['mesh']]
    fixtures=[b for b in bindings if '_efixturetype_' in b['mesh']]
    rigid=[b for b in bindings if rigid_kind(b['mesh'])]
    origin_z=sum(w['position_beamng_m'][2] for w in manifest['wheels'])/4
    origin=np.array([0,0,origin_z]);manifest['origin_beamng_m']=origin.tolist()
    if not shell:raise ValueError('No main body mesh binding found')
    allbody=[];fixture_report=[];fixture_root=node('Fixtures');body.children.append(fixture_root)
    for b in shell+fixtures:
        if b['mesh'] not in geos:raise ValueError('Missing visible mesh '+b['mesh'])
        rot=beam_rotation([b['rot'][a] for a in 'xyz']);scale=np.array([b['scale'][a] for a in 'xyz']);pos=np.array([b['pos'][a] for a in 'xyz'])
        if np.any(np.abs(scale)<1e-8):raise ValueError('Zero scale on visible mesh '+b['mesh'])
        fixture=b in fixtures;parent=body
        if fixture:
            parent=node(b['mesh']);parent.transform.localPosition=tuple(unity_to_mu(beam_to_unity((pos-origin).reshape(1,3)))[0]);fixture_root.children.append(parent)
        def transform(v,n):
            world=beam_to_unity((v*scale)@rot.T+pos-origin);allbody.append(world)
            v=beam_to_unity((v*scale)@rot.T) if fixture else world
            n=beam_to_unity((n/scale)@rot.T);n/=np.maximum(1e-9,np.linalg.norm(n,axis=1))[:,None];return v,n
        renderers=add_groups(parent,geos[b['mesh']],transform,materials,b['mesh'])
        if fixture:
            fixture_report.append({'id':b['mesh'],'object_path':'body/Fixtures/'+b['mesh'],'source_transform':{k:b[k] for k in ('pos','rot','scale')},'pivot_unity_m':beam_to_unity(pos-origin).tolist(),'rotation_scale_baked_into_mesh':True,'renderer_count':renderers,'materials':sorted({g['material'] for g in geos[b['mesh']]}),'triangles':sum(len(g['positions'])//3 for g in geos[b['mesh']])})
    bounds=np.vstack(allbody);lo=bounds.min(axis=0);hi=bounds.max(axis=0)
    # Single modest chassis box is lifted above the tires' contact plane;
    # corners/body cosmetic panels are not individual collision parts.
    box=node('chassisCollider');box.collider=mu.MuColliderBox(True);box.collider.isTrigger=0
    ground=max(-w['position_beamng_m'][2]+origin_z+w['radius_m'] for w in manifest['wheels']);size=hi-lo
    center=(lo+hi)/2;center[2]=min(center[2],ground-.20);size[0]*=.8;size[1]*=.82;size[2]=min(size[2]*.6,.55)
    box.collider.center=tuple(unity_to_mu(center.reshape(1,3))[0]);box.collider.size=tuple(unity_to_mu(size.reshape(1,3))[0]);body.children.append(box)
    # Add after the collider bounds calculation: artwork must not change contact
    # geometry, mass, wheel placement or the chassis stability calibration.
    hardware=node('RigidHardware');body.children.append(hardware);rigid_report=[]
    for b in rigid:
        if b['mesh'] not in geos:raise ValueError('Missing rigid hardware mesh '+b['mesh'])
        rot=beam_rotation([b['rot'][a] for a in 'xyz']);scale=np.array([b['scale'][a] for a in 'xyz']);pos=np.array([b['pos'][a] for a in 'xyz'])
        if np.any(np.abs(scale)<1e-8):raise ValueError('Zero scale on rigid mesh '+b['mesh'])
        parent=node(b['mesh']);parent.transform.localPosition=tuple(unity_to_mu(beam_to_unity((pos-origin).reshape(1,3)))[0]);hardware.children.append(parent)
        def transform(v,n):
            v=beam_to_unity((v*scale)@rot.T);n=beam_to_unity((n/scale)@rot.T);n/=np.maximum(1e-9,np.linalg.norm(n,axis=1))[:,None];return v,n
        count=add_groups(parent,geos[b['mesh']],transform,materials,b['mesh'])
        rigid_report.append({'mesh':b['mesh'],'kind':rigid_kind(b['mesh']),'source_transform':{k:b[k] for k in ('pos','rot','scale')},'renderer_count':count,'triangles':sum(len(g['positions'])//3 for g in geos[b['mesh']]),'cosmetic_only':True})
    suspension_report=[]
    for b in bindings:
        if '_suspension_' not in b['mesh'] and '_suspensionm_' not in b['mesh']:continue
        if b['mesh'] not in geos:raise ValueError('Missing suspension mesh '+b['mesh'])
        rot=beam_rotation([b['rot'][a] for a in 'xyz']);scale=np.array([b['scale'][a] for a in 'xyz']);pos=np.array([b['pos'][a] for a in 'xyz'])
        if np.any(np.abs(scale)<1e-8):raise ValueError('Zero scale on suspension mesh '+b['mesh'])
        points=np.vstack([(g['positions']*scale)@rot.T+pos for g in geos[b['mesh']]])
        corner=suspension_corner(b,manifest['wheels'],manifest.get('suspension_visual_anchors',{}),points)
        if corner:
            parent=next((c for c in body.children if c.transform.name=='KDASuspension_'+corner),None)
            if parent is None:parent=node('KDASuspension_'+corner);body.children.append(parent)
        else:
            # Live axles use axle_R/F rather than separate hub/subframe groups.
            # Never discard the exported geometry or attach a shared axle to
            # one wheel. Preserve its placement as cosmetic rigid hardware.
            parent=node('KDARigidSuspension_'+b['mesh']);hardware.children.append(parent)
            manifest['warnings'].append('Suspension mesh '+b['mesh']+' retained as rigid visual geometry; this layout has no independent-wheel animation anchors.')
        def transform(v,n):
            v=beam_to_unity((v*scale)@rot.T+pos-origin);n=beam_to_unity((n/scale)@rot.T);n/=np.maximum(1e-9,np.linalg.norm(n,axis=1))[:,None];return v,n
        count=add_groups(parent,geos[b['mesh']],transform,materials,b['mesh'])
        suspension_report.append({'mesh':b['mesh'],'corner':corner,'animated':bool(corner),'renderer_count':count,'triangles':sum(len(g['positions'])//3 for g in geos[b['mesh']]),'cosmetic_only':True})
    # Geometry-only cockpit/rear-lamp preparation. Physics collider bounds
    # above and all materials/triangles remain unchanged.
    cosmetic=mu.Mu('body');cosmetic.obj=body;cosmetic.materials=materials.materials
    from .interior import rig
    from .lighting import isolate_rear
    manifest['interior']=rig(cosmetic,manifest);isolate_rear(cosmetic,manifest)
    models['body']=body
    for wheel in manifest['wheels']:
        corner=wheel['corner'];o=node('wheel_'+corner);collider=node('wheelCollider');susp=node('suspensionPivot');steering=node('SteeringPivot');spin=node('wheelPivot');o.children=[collider,susp];susp.children=[steering];steering.children=[spin]
        # KSP normalizes a MODEL root transform. Keep the physics rig identity
        # and bake only the corner mount into vertices; a root-yaw compensation
        # in the mesh survives normalization and incorrectly faces rims inward.
        hub=beam_to_unity(np.array(wheel['position_beamng_m']));left=hub[0]<0;mount=rotation([-90,0,0])@(rotation([0,180,0]) if left else np.eye(3))
        for name in (wheel['tire_mesh'],wheel['rim_mesh']):
            b=next(b for b in bindings if b['mesh']==name and (b['pos']['x']>0)==(wheel['position_beamng_m'][0]>0));rot=wheel_rotation([b['rot'][a] for a in 'xyz']);pos=np.array([b['pos'][a] for a in 'xyz']);scale=np.array([b['scale'][a] for a in 'xyz'])
            def transform(v,n):return (beam_to_unity((v*scale)@rot.T+pos)-hub)@mount,beam_to_unity(n@rot.T)@mount
            add_groups(spin,geos[name],transform,materials,name)
        # Calipers follow travel/steering, while discs follow wheel rotation.
        # Reuse the wheel rig and straight axle alignment: no extra physics.
        for b in bindings:
            kind='disc' if '_brakedisc_' in b['mesh'] else 'caliper' if '_brakecaliper_' in b['mesh'] else None
            if not kind or b['mesh'] not in geos:continue
            axle_group='hub_F' if wheel['axle']=='front' else 'hub_R'
            groups=b.get('node_groups',[])
            if kind=='disc' and 'wheel_'+corner not in groups:continue
            if kind=='caliper' and (axle_group not in groups or (b['pos']['x']>0)!=(wheel['position_beamng_m'][0]>0)):continue
            rot=(wheel_rotation if kind=='disc' else beam_rotation)([b['rot'][a] for a in 'xyz']);pos=np.array([b['pos'][a] for a in 'xyz']);scale=np.array([b['scale'][a] for a in 'xyz'])
            def transform(v,n):
                p=(beam_to_unity((v*scale)@rot.T+pos)-hub)@mount
                n=beam_to_unity((n/scale)@rot.T)@mount;n/=np.maximum(1e-9,np.linalg.norm(n,axis=1))[:,None];return p,n
            parent=spin if kind=='disc' else steering
            add_groups(parent,geos[b['mesh']],transform,materials,b['mesh'])
        # Stock wheel contact colliders are created only in flight. Supply a
        # dedicated editor pick volume around the actual wheel meshes.
        verts=[]
        def wheel_vertices(obj):
            if hasattr(obj,'shared_mesh'):verts.extend(obj.shared_mesh.verts)
            for child in obj.children:wheel_vertices(child)
        wheel_vertices(spin)
        points=np.array(verts);wlo=points.min(axis=0);whi=points.max(axis=0)
        pick=node('kdaSelectionCollider');pick.collider=mu.MuColliderBox(True)
        pick.collider.isTrigger=0;pick.collider.center=tuple((wlo+whi)/2)
        pick.collider.size=tuple(np.maximum((whi-wlo)*.98,.02))
        spin.children.append(pick)
        models[corner]=o
    model_materials={}
    for name,o in models.items():
        import copy
        renderers=[]
        def collect(obj):
            if hasattr(obj,'renderer'):renderers.append(obj.renderer)
            for child in obj.children:collect(child)
        collect(o);used=sorted({i for renderer in renderers for i in renderer.materials});mapping={old:new for new,old in enumerate(used)}
        localmats=[copy.deepcopy(materials.materials[i]) for i in used];texused=sorted({prop.index for mat in localmats for prop in mat.textureProperties.values()});texmap={old:new for new,old in enumerate(texused)}
        for renderer in renderers:renderer.materials=[mapping[i] for i in renderer.materials]
        for mat in localmats:
            for prop in mat.textureProperties.values():prop.index=texmap[prop.index]
        model_materials[name]=[mat.name for mat in localmats]
        model=mu.Mu(name);model.obj=o;model.materials=localmats;model.textures=[materials.textures[i] for i in texused];model.write(str(output/(name+'.mu')))
    body_materials=sorted({g['material'] for b in shell for g in geos[b['mesh']]})
    manifest['visual']={'models':['Models/'+name+'.mu' for name in models],'body_bounds_m':[lo.tolist(),hi.tolist()],'material_count':len(materials.materials),'texture_limit':materials.limit,'suspension_hardware_omitted':False,'suspension_hardware':suspension_report,'suspension_import_version':2,'suspension_animation_version':1,'suspension_animation_basis':'Two-bone visual travel skin weighted by nearest exported fixed/hub nodes; unsupported layouts retained as rigid geometry; no steering or physics solver','rigid_hardware':rigid_report,'rigid_hardware_version':1,'material_pipeline_version':7,'model_materials':model_materials,'optional_pbr_backend':'Resurfaced+Shabby','wheel_alignment_version':2,'wheel_alignment_basis':'Imported camber/toe removed from spinning meshes; axle roll and side mirroring retained; KDA part toe controls alignment','wheel_selection_colliders':True,'fixture_pipeline_version':1,'fixtures':fixture_report,'body_material_groups':body_materials,'material_conversion':materials.report}
    return models
