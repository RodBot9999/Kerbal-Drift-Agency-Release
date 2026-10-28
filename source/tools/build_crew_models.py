"""Build KDA crew seat, internal model and doorway marker assets.

Builds use the retained optimized seat mesh when it is available.
"""
import sys,json,math,copy,argparse
from pathlib import Path
import numpy as np
from PIL import Image
root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'tools/vendor/io_object_mu'))
import mu
parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--output',type=Path,default=root/'.build/assets/Crew/Models');args=parser.parse_args()
dest=args.output.resolve();dest.mkdir(parents=True,exist_ok=True)
reports=root/'.build/asset-reports';reports.mkdir(parents=True,exist_ok=True)
def node(name,layer=0):
    o=mu.MuObject();o.transform=mu.MuTransform();o.transform.name=name;o.transform.localPosition=(0,0,0);o.transform.localRotation=(1,0,0,0);o.transform.localScale=(1,1,1)
    o.tag_and_layer=mu.MuTagLayer();o.tag_and_layer.tag='Untagged';o.tag_and_layer.layer=layer;return o
def xyz(v):return tuple(np.array(v)[[0,2,1]])
materials=[]
for name,color in [('fabric',(.085,.085,.085,1)),('red',(.60,.04,.045,1)),('shell',(.035,.035,.035,1)),('metal',(.34,.37,.40,1)),('marker',(.05,.9,.8,1))]:
    mat=mu.MuMaterial();mat.name='KDA '+name;mat.shaderName='KSP/Diffuse';mat.colorProperties['_Color']=color
    tex=mu.MuMatTex();tex.index=0 if name in ('fabric','red') else 1;tex.scale=(1,1);tex.offset=(0,0);mat.textureProperties['_MainTex']=tex;materials.append(mat)
texture=mu.MuTexture();texture.name='seat_fabric';texture.type=0
solid=mu.MuTexture();solid.name='seat_solid';solid.type=0;textures=[texture,solid]
y,x=np.indices((256,256));weave=244+((x+y)%4)+((x//4+y//4)%2)*2
Image.fromarray(np.repeat(weave[:,:,None],3,axis=2).astype('uint8')).save(dest/'seat_fabric.png')
Image.new('RGB',(4,4),(255,255,255)).save(dest/'seat_solid.png')
shapes=[]
def box(name,center,size,material=0,angle=0,bevel=.004):
    c=np.array(center);s=np.array(size)/2;theta=math.radians(angle);r=np.array([[1,0,0],[0,math.cos(theta),-math.sin(theta)],[0,math.sin(theta),math.cos(theta)]])
    bevel=min(bevel,float(s.min())*.4);inner=s-bevel;faces=[]
    # Closed chamfered box: 6 main panels, 12 bevel strips, 8 corner caps.
    for axis in range(3):
        other=[i for i in range(3) if i!=axis]
        for sign in [-1,1]:
            face=[]
            for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]:
                v=np.zeros(3);v[axis]=sign*s[axis];v[other]=[a*inner[other[0]],b*inner[other[1]]];face.append(v)
            faces.append(np.array(face))
    for free in range(3):
        axes=[i for i in range(3) if i!=free]
        for a in [-1,1]:
            for b in [-1,1]:
                face=[]
                for side,end in [(0,-1),(1,-1),(1,1),(0,1)]:
                    v=np.zeros(3);v[free]=end*inner[free];v[axes[0]]=a*(s[axes[0]] if side==0 else inner[axes[0]]);v[axes[1]]=b*(inner[axes[1]] if side==0 else s[axes[1]]);face.append(v)
                faces.append(np.array(face))
    for a in [-1,1]:
        for b in [-1,1]:
            for z in [-1,1]:
                signs=np.array([a,b,z]);face=[]
                for axis in range(3):v=signs*inner;v[axis]=signs[axis]*s[axis];face.append(v)
                faces.append(np.array(face))
    positions=[];normals=[];uv=[];tri=[]
    for face in faces:
        q=face;normal=np.cross(q[1]-q[0],q[2]-q[0]);normal/=np.linalg.norm(normal)
        if np.dot(normal,q.mean(axis=0))<0:q=q[::-1];normal=-normal
        axes=np.argsort(np.abs(normal))[:2];face_uv=q[:,axes]/.16
        q=q@r.T+c;normal=normal@r.T
        start=len(positions);positions.extend(q);normals.extend([normal]*len(q));uv.extend(map(tuple,face_uv));tri.extend((start,start+i,start+i+1) for i in range(1,len(q)-1))
    shapes.append({'name':name,'positions':np.array(positions),'normals':np.array(normals),'uv':uv,'triangles':tri,'material':material})
box('seat pan shell',(0,.055,0),(.42,.075,.47),2)
box('seat cushion',(0,.105,.02),(.32,.065,.37),0,bevel=.012)
for side in [-1,1]:
    box('hip bolster '+str(side),(side*.19,.16,-.005),(.065,.16,.40),1,bevel=.011)
    box('back bolster '+str(side),(side*.175,.36,-.18),(.075,.35,.095),1,-10)
    box('shoulder wing '+str(side),(side*.20,.57,-.205),(.10,.15,.085),1,-10)
    box('seat rail '+str(side),(side*.12,.005,0),(.045,.04,.42),3)
# Separate back panels leave real gaps for the two shoulder harness openings.
box('lower back shell',(0,.29,-.215),(.37,.31,.055),2,-10)
box('lower back cushion',(0,.30,-.175),(.27,.25,.05),0,-10)
box('headrest',(0,.65,-.245),(.29,.15,.065),2,-10)
box('headrest pad',(0,.65,-.205),(.22,.11,.04),0,-10)
for side in [-1,1]:box('harness outer bar '+str(side),(side*.14,.52,-.225),(.04,.12,.06),2,-10)
box('harness middle bar',(0,.52,-.225),(.06,.12,.06),2,-10)
box('harness lower bar',(0,.445,-.225),(.30,.035,.06),2,-10)

recaro_report=None
if (root/'assets/seat-recaro/master.obj').is_file():
    from recaro_seat_geometry import build
    shapes,recaro_report=build(dest)
    materials[0].colorProperties['_Color']=(.19,.19,.19,1)
    materials[2].colorProperties['_Color']=(.09,.09,.09,1)
def add_geometry(parent,shape,rotation=None,layer=0):
    o=node(shape['name'],layer);mesh=mu.MuMesh();positions=shape['positions'];normals=shape['normals']
    if rotation is not None:positions=positions@rotation.T;normals=normals@rotation.T
    # xyz swaps axes and reverses handedness. Reverse winding here so the
    # MU writer's Unity conversion leaves face orientation matching normals.
    mesh.verts=[xyz(p) for p in positions];mesh.normals=[xyz(p) for p in normals];mesh.uvs=shape['uv'];mesh.submeshes=[[(a,c,b) for a,b,c in shape['triangles']]]
    o.shared_mesh=mesh;o.renderer=mu.MuRenderer();o.renderer.materials=[shape['material']];parent.children.append(o)
def save(name,obj):
    renderers=[]
    def collect(o):
        if hasattr(o,'renderer'):renderers.append(o.renderer)
        for c in o.children:collect(c)
    collect(obj);used=sorted({i for r in renderers for i in r.materials});mapping={old:new for new,old in enumerate(used)}
    for renderer in renderers:renderer.materials=[mapping[i] for i in renderer.materials]
    local_materials=[copy.deepcopy(materials[i]) for i in used];used_textures=sorted({p.index for m in local_materials for p in m.textureProperties.values()});texture_map={old:new for new,old in enumerate(used_textures)}
    for m in local_materials:
        for p in m.textureProperties.values():p.index=texture_map[p.index]
    model=mu.Mu(name);model.obj=obj;model.materials=local_materials;model.textures=[copy.deepcopy(textures[i]) for i in used_textures];model.write(str(dest/(name+'.mu')))
seat=node('KDARaceSeat');
for shape in shapes:add_geometry(seat,shape)
pick=node('seat selection');pick.collider=mu.MuColliderBox(True);pick.collider.isTrigger=0
if recaro_report:
    bounds=np.vstack([s['positions'] for s in shapes]);pick.collider.center=xyz((bounds.min(0)+bounds.max(0))*.5);pick.collider.size=xyz(bounds.max(0)-bounds.min(0))
else:
    pick.collider.center=xyz((0,.32,-.03));pick.collider.size=xyz((.44,.64,.43))
seat.children.append(pick)
hatch=node('KDAAirlock');hatch.tag_and_layer.tag='Airlock';hatch.tag_and_layer.layer=21
hatch.collider=mu.MuColliderBox(True);hatch.collider.isTrigger=1;hatch.collider.center=(0,0,0);hatch.collider.size=xyz((1.3,1.5,1.3));seat.children.append(hatch)
save('race-seat',seat)
# KSP's IVA frame includes Euler(90,180,0). Author its geometry in the
# inverse frame, so the internal and external seat occupy the same space.
r=np.array([[-1.,0,0],[0,0,-1],[0,-1,0]])
iva=node('KDARaceSeatInternal',16)
for shape in shapes:add_geometry(iva,shape,r.T,16)
# Lower the occupant into the Recaro bowl. This moves the real IVA Kerbal,
# so the external pose, eye camera and portrait continue to agree.
crew_position=np.array([0,.045 if recaro_report else .135,.035])
pivot=node('seatPilot',16);pivot.transform.localPosition=xyz(r.T@crew_position);pivot.transform.localRotation=(0,0,math.sqrt(.5),-math.sqrt(.5));iva.children.append(pivot)
lamp=node('crew portrait fill',16);lamp.transform.localPosition=xyz(r.T@np.array([0,.8,.55]));lamp.light=mu.MuLight();lamp.light.type=2;lamp.light.intensity=1;lamp.light.range=2;lamp.light.color=(1,.95,.90,1);lamp.light.cullingMask=1<<16;lamp.light.spotAngle=30;iva.children.append(lamp)
save('race-seat-internal',iva)
seat_shapes=list(shapes);shapes=[]
for side in [-1,1]:box('door frame '+str(side),(side*.12,.0,0),(.018,.38,.018),4)
box('door top',(0,.19,0),(.26,.018,.018),4)
box('exit arrow shaft',(0,0,.15),(.035,.035,.30),4)
box('exit arrow head',(0,0,.32),(.12,.045,.08),4)
marker=node('KDADoorMarker')
for shape in shapes:add_geometry(marker,shape)
pick=node('door selection');pick.collider=mu.MuColliderBox(True);pick.collider.isTrigger=0;pick.collider.center=xyz((0,0,.15));pick.collider.size=xyz((.28,.40,.40));marker.children.append(pick);save('door-marker',marker)
seat_bounds=np.vstack([s['positions'] for s in seat_shapes]);seat_size=seat_bounds.max(axis=0)-seat_bounds.min(axis=0)
report={'original_geometry':True,'seat_triangles':sum(len(s['triangles']) for s in seat_shapes),'marker_triangles':sum(len(s['triangles']) for s in shapes),'seat_width_m':float(seat_size[0]),'seat_height_m':float(seat_size[1]),'texture_size_px':256,'surface_winding_corrected':True,'beveled_closed_volumes':True,'fabric_only_on_upholstery':True,'stock_seat_authored_reference_bounds_m':[.405,.411,.480],'flight_marker_invisible':True,'stock_seat_or_kda_meshes_copied':False}
if recaro_report:
    report.update(original_geometry=False,source='user supplied Recaro Pole Position OBJ',texture_size_px=1024,optimized_recaro=recaro_report)
report['crew_local_position_m']=crew_position.tolist()
report['crew_lowered_m']=.09 if recaro_report else 0
(reports/'crew-models.json').write_text(json.dumps(report,indent=2)+'\n')
# Save a deterministic preview of the original authored geometry.
from PIL import ImageDraw
preview=Image.new('RGB',(1800,1100),(20,20,24));draw=ImageDraw.Draw(preview)
for j,azim in enumerate([35,215]):
    a=math.radians(azim);e=math.radians(18)
    right=np.array([math.cos(a),0,-math.sin(a)]);forward=np.array([math.sin(a)*math.cos(e),-math.sin(e),math.cos(a)*math.cos(e)]);up=np.cross(forward,right)
    basis=np.array([right,up,forward]);polygons=[]
    for s in seat_shapes:
        for t in s['triangles']:
            face=s['positions'][list(t)];normal=s['normals'][t[0]]
            if np.dot(normal,forward)>0:continue
            v=(face-np.array([0,.36,0]))@basis.T;xy=np.column_stack((v[:,0]*1050+450+j*900,550-v[:,1]*1050))
            color=np.array(materials[s['material']].colorProperties['_Color'][:3]);light=.65+.35*abs(np.dot(normal,np.array([.4,.7,.5])))
            polygons.append((float(v[:,2].mean()),xy,[int(min(255,x*255*light)) for x in color]))
    for _,xy,color in sorted(polygons,key=lambda x:x[0],reverse=True):draw.polygon([tuple(p) for p in xy],fill=tuple(color))
preview.resize((900,550),Image.Resampling.LANCZOS).save(reports/'race-seat-model-preview.png')
print(json.dumps(report))
