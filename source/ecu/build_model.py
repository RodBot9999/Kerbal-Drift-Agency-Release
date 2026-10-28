# SPDX-License-Identifier: GPL-3.0-only
"""Build an original low-poly surface-mounted ECU. Uses the repository MU writer."""
from pathlib import Path
import sys, json
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'source/tools/vendor/io_object_mu'))
import mu
OUT=ROOT/'GameData/KDAVehicles/Addons/ECU/Models'
OUT.mkdir(parents=True,exist_ok=True)
def xyz(p):return tuple(np.asarray(p)[[0,2,1]])
def node(name):
 o=mu.MuObject(name);o.transform=mu.MuTransform();o.transform.name=name;o.transform.localPosition=(0,0,0);o.transform.localRotation=(1,0,0,0);o.transform.localScale=(1,1,1);o.tag_and_layer=mu.MuTagLayer();o.tag_and_layer.tag='Untagged';o.tag_and_layer.layer=0;return o
materials=[]
for name,color,texture in [('anodized housing',(.085,.10,.115,1),0),('aluminum fins',(.30,.34,.37,1),0),('connectors',(.025,.03,.035,1),0),('contact pins',(.68,.53,.24,1),0),('lid label',(1,1,1,1),1)]:
 m=mu.MuMaterial();m.name='KDA ECU '+name;m.shaderName='KSP/Diffuse';m.colorProperties['_Color']=color;t=mu.MuMatTex();t.index=texture;t.scale=(1,1);t.offset=(0,0);m.textureProperties['_MainTex']=t;materials.append(m)
textures=[]
for name in ('ecu-solid','ecu-label'):
 t=mu.MuTexture();t.name=name;t.type=0;textures.append(t)
Image.new('RGB',(4,4),(255,255,255)).save(OUT/'ecu-solid.png')
im=Image.new('RGB',(512,384),(21,27,31));d=ImageDraw.Draw(im)
font_path=Path('C:/Windows/Fonts/arialbd.ttf');font=ImageFont.truetype(str(font_path),52);small=ImageFont.truetype(str(font_path),24)
d.rounded_rectangle((5,5,506,378),radius=12,outline=(70,211,191),width=5)
d.text((26,22),'KDA / ECU',font=font,fill=(224,240,241));d.text((28,91),'DRIVE BY WIRE',font=small,fill=(82,224,201));d.text((28,139),'SC  /  ABS  /  ROVER',font=small,fill=(176,190,195))
d.polygon([(366,190),(426,190),(426,278),(465,278),(396,350),(327,278),(366,278)],fill=(70,211,191))
d.text((24,235),'FORWARD',font=small,fill=(224,240,241));d.text((24,275),'RODERIC',font=small,fill=(176,190,195));d.text((24,305),'SYSTEMS',font=small,fill=(176,190,195));im.save(OUT/'ecu-label.png')
root=node('KDA_Rover_ECU');shapes=[]
def box(name,center,size,material):
 c=np.array(center);s=np.array(size)*.5;verts=[];normals=[];uv=[];tris=[]
 for axis in range(3):
  other=[a for a in range(3) if a!=axis]
  for sign in (-1,1):
   q=[]
   for a,b in ((-1,-1),(1,-1),(1,1),(-1,1)):
    p=np.zeros(3);p[axis]=sign*s[axis];p[other]=[a*s[other[0]],b*s[other[1]]];q.append(p)
   q=np.array(q);n=np.cross(q[1]-q[0],q[2]-q[0]);n/=np.linalg.norm(n)
   if np.dot(n,q.mean(axis=0))<0:q=q[::-1];n=-n
   start=len(verts);verts.extend(q+c);normals.extend([n]*4);uv.extend([(0,0),(1,0),(1,1),(0,1)]);tris.extend([(start,start+1,start+2),(start,start+2,start+3)])
 # Unity's clockwise face winding needs the label's U axis reversed. Keep V
 # unchanged so the existing FORWARD arrow continues to point along ECU +Z.
 if material==4:uv=[(1-u,v) for u,v in uv]
 o=node(name);mesh=mu.MuMesh();mesh.verts=[xyz(v) for v in verts];mesh.normals=[xyz(n) for n in normals];mesh.uvs=uv;mesh.submeshes=[[(a,c,b) for a,b,c in tris]];o.shared_mesh=mesh;o.renderer=mu.MuRenderer();o.renderer.materials=[material];root.children.append(o);shapes.append(dict(name=name,center=center,size=size,material=material))
box('sealed ECU case',(0,0,0),(.28,.044,.205),0)
box('lid label',(0,.023,0),(.212,.002,.184),4)
for side in (-1,1):
 for i in range(8):box('cooling fin',(side*.124,.027,-.079+i*.022),(.025,.012,.006),1)
 for z in (-.075,.075):
  box('mounting tab',(side*.153,-.018,z),(.035,.008,.033),1)
  box('mounting screw',(side*.153,-.012,z),(.010,.007,.010),2)
for x in (-.065,.065):
 box('weatherproof connector',(x,-.001,-.119),(.099,.034,.038),2)
 box('connector latch',(x,.021,-.115),(.052,.011,.021),1)
 for i in range(6):box('connector pin',(x-.036+i*.014,-.001,-.141),(.005,.013,.006),3)
pick=node('ECU case collider');pick.collider=mu.MuColliderBox(True);pick.collider.isTrigger=0;pick.collider.center=xyz((0,.001,-.012));pick.collider.size=xyz((.34,.058,.27));root.children.append(pick)
model=mu.Mu('ecu');model.obj=root;model.materials=materials;model.textures=textures;model.write(str(OUT/'ecu.mu'))
loaded=mu.Mu();assert loaded.read(str(OUT/'ecu.mu'))
report=dict(meshes=len(shapes),triangles=len(shapes)*12,size_m=[.34,.058,.27],original_asset=True,shapes=shapes)
REPORT=ROOT/'source/.build/ecu';REPORT.mkdir(parents=True,exist_ok=True);(REPORT/'model-report.json').write_text(json.dumps(report,indent=2))
# A review image from the same authored geometry, not a KSP screenshot.
canvas=Image.new('RGB',(960,600),(24,32,37));draw=ImageDraw.Draw(canvas)
eye=np.array([1.,1.4,-1.8]);eye/=np.linalg.norm(eye);right=np.cross(np.array([0.,1.,0.]),eye);right/=np.linalg.norm(right);up=np.cross(eye,right)
light=np.array([-.3,1,-.7]);light/=np.linalg.norm(light)
colors=np.array([[40,52,59],[115,130,140],[17,25,29],[176,149,80],[33,191,169]])
faces=[]
for shape in shapes:
 c=np.array(shape['center']);half=np.array(shape['size'])/2;v=np.array([[a,b,z] for a in (-1,1) for b in (-1,1) for z in (-1,1)])*half+c
 for indices in [(0,1,3,2),(4,5,7,6),(0,1,5,4),(2,3,7,6),(0,2,6,4),(1,3,7,5)]:
  f=v[list(indices)];normal=np.cross(f[1]-f[0],f[2]-f[0]);normal/=np.linalg.norm(normal)
  if np.dot(normal,f.mean(axis=0)-c)<0:normal=-normal
  if np.dot(normal,eye)<=0:continue
  points=[(480+2200*np.dot(p,right),315-2200*np.dot(p,up)) for p in f]
  color=tuple((colors[shape['material']]*(.55+.45*max(0,np.dot(normal,light)))).astype(int))
  faces.append((np.dot(f.mean(axis=0),eye),points,color))
for _,points,color in sorted(faces,key=lambda f:f[0]):draw.polygon(points,fill=color,outline=(23,30,34))
draw.text((30,24),'KDA ROVER ECU',font=font,fill=(218,238,238));draw.text((32,90),'Original model / geometry preview',font=small,fill=(82,224,201));canvas.save(REPORT/'ecu-model-preview.png')
print(json.dumps({k:v for k,v in report.items() if k!='shapes'}))
