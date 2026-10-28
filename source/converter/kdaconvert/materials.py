"""Bake BeamNG material layers into textures supported by stock KSP shaders."""
from pathlib import Path
import hashlib
import re
import numpy as np
from PIL import Image
from .automation import relaxed_json
from .resurfaced import bake_surface


def sample(image, uv):
    """Bilinear, repeating texture lookup; COLLADA UV origin is bottom left."""
    h,w=image.shape[:2]
    x=(uv[...,0]%1)*w-.5; y=(1-uv[...,1]%1)*h-.5
    xi=np.floor(x).astype(int); yi=np.floor(y).astype(int)
    fx=(x-xi)[...,None]; fy=(y-yi)[...,None]
    return ((image[yi%h,xi%w]*(1-fx)+image[yi%h,(xi+1)%w]*fx)*(1-fy)
            +(image[(yi+1)%h,xi%w]*(1-fx)+image[(yi+1)%h,(xi+1)%w]*fx)*fy)


def reproject(image, source_uv, target_uv, size):
    """Rasterize UV triangles to move a texture onto the opacity atlas."""
    out=np.zeros((size,size,4),dtype=np.float32)
    covered=np.zeros((size,size),dtype=bool)
    for src,dst in zip(source_uv.reshape(-1,3,2),target_uv.reshape(-1,3,2)):
        p=dst.copy();p[:,0]=p[:,0]*size-.5;p[:,1]=(1-p[:,1])*size-.5
        lo=np.maximum(0,np.floor(p.min(axis=0)).astype(int));hi=np.minimum(size-1,np.ceil(p.max(axis=0)).astype(int))
        if np.any(hi<lo):continue
        a,b,c=p;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-9:continue
        yy,xx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
        u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
        v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den
        inside=(u>=-1e-5)&(v>=-1e-5)&(u+v<=1.00001)
        uv=u[...,None]*src[0]+v[...,None]*src[1]+(1-u-v)[...,None]*src[2]
        out[yy[inside],xx[inside]]=sample(image,uv[inside]);covered[yy[inside],xx[inside]]=True
    # Pad atlas islands by two pixels to avoid dark seams under bilinear filtering.
    for _ in range(2):
        old=covered.copy()
        for dy,dx in ((-1,0),(1,0),(0,-1),(0,1)):
            neighbour=np.roll(old,(dy,dx),(0,1));valid=neighbour&~covered
            if dy<0:valid[-1]=False
            if dy>0:valid[0]=False
            if dx<0:valid[:,-1]=False
            if dx>0:valid[:,0]=False
            out[valid]=np.roll(out,(dy,dx),(0,1))[valid];covered[valid]=True
    return out

def resized(image,size):
    if image.shape[:2]==(size,size):return image.copy()
    return np.stack([np.asarray(Image.fromarray(image[...,c],mode='F').resize((size,size),Image.Resampling.BILINEAR)) for c in range(4)],axis=-1)

def on_uv(image,source,target,size,groups):
    flat=float(np.max(np.ptp(image,axis=(0,1))))<.005
    if source==target or flat:return resized(image,size)
    return reproject(image,np.concatenate([g['uv'+str(source)] for g in groups]),np.concatenate([g['uv'+str(target)] for g in groups]),size)


class Materials:
    def __init__(self,vehicle,output,quality,mu,geos,lighting=None,paint_color=None):
        self.light_materials={x['material']:x for x in (lighting or {}).get('materials',[])}
        self.vehicle=vehicle;self.output=output;self.mu=mu
        self.limit={'quality':2048,'balanced':1024,'lightweight':512}[quality]
        self.materials=[];self.textures=[];self.cache={};self.source={};self.report=[];self.images={};self.texture_cache={}
        self.files={p.name.lower():p for p in vehicle.rglob('*.dds')}
        for p in vehicle.rglob('*.materials.json'):self.source.update(relaxed_json(p.read_text(encoding='utf-8')))
        self.paints=relaxed_json((vehicle/'info.json').read_text(encoding='utf-8'))['paints']
        if paint_color:
            if not re.fullmatch(r'#[0-9a-fA-F]{6}',paint_color):raise ValueError('Body paint must be a #RRGGBB color')
            self.paints['Default1']=dict(self.paints['Default1'],baseColor=[int(paint_color[i:i+2],16)/255 for i in (1,3,5)]+[1])
        self.groups={}
        uses={}
        for mesh,groups in geos.items():
            for group in groups:uses.setdefault(group['material'],set()).add(mesh.lower())
        hardware=lambda mesh:any(s in mesh for s in ('_chassis','_engine','_exhaust','_mainexhaustmesh','_suspension'))
        self.hardware_materials={name for name,meshes in uses.items() if all(hardware(mesh) for mesh in meshes) and not any(s in name.lower() for s in ('chrome','shiny','polish'))}
        for groups in geos.values():
            for group in groups:self.groups.setdefault(group['material'],[]).append(group)

    def image(self,source):
        key=Path(source).name.lower()
        if key not in self.images:
            if key not in self.files:raise ValueError('Missing texture '+source)
            image=Image.open(self.files[key]).convert('RGBA');image.thumbnail((self.limit,self.limit),Image.Resampling.LANCZOS)
            self.images[key]=np.asarray(image,dtype=np.float32)/255
        return self.images[key]

    def texture(self,image,name,kind=0):
        pixels=np.clip(image*255,0,255).astype('uint8')
        key=(kind,pixels.shape,hashlib.sha256(pixels.tobytes()).digest())
        if key in self.texture_cache:return self.texture_cache[key]
        Image.fromarray(pixels).save(self.output/(name+'.png'))
        texture=self.mu.MuTexture();texture.name=name;texture.type=kind
        index=len(self.textures);self.textures.append(texture);self.texture_cache[key]=index
        return index

    def get(self,name):
        if name in self.cache:return self.cache[name]
        desc=self.source.get(name,{});stage=desc.get('Stages',[{}])[0]
        source=stage.get('diffuseMap',stage.get('baseColorMap'));opacity=stage.get('opacityMap')
        duv=int(stage.get('diffuseMapUseUV',stage.get('baseColorMapUseUV',0)));ouv=int(stage.get('opacityMapUseUV',0))
        target=ouv if opacity else duv
        # These exported height maps encode the solid wires in white and the
        # grille openings in black. Never infer holes from ordinary dark paint.
        perforated=bool(source and re.fullmatch(r'camso_t_grille\d+_h_stepped\.dds',Path(source).name.lower()) and 'grille' in name.lower())
        groups=self.groups.get(name,[])
        if perforated and groups:
            # Give tiled grille detail its own atlas: a narrow island in the
            # body stamp otherwise reduces dozens of wire cells to a few pixels.
            coords=np.concatenate([g['uv'+str(target)] for g in groups])
            lo=coords.min(axis=0);span=np.maximum(coords.max(axis=0)-lo,1e-8)
            for g in groups:g['uv2']=(g['uv'+str(target)]-lo)/span*.98+.01
            target=2
        cutout=bool(opacity or perforated)
        color=np.array(stage.get('diffuseColor',[1,1,1,1]),dtype=np.float32)
        paint=None;palette_slot=None;palette=None
        if stage.get('instanceDiffuse'):
            palette=stage.get('colorPaletteMap')
            # Automation exports solid channel masks for its paint slots.
            weights=self.image(palette)[...,:3].mean(axis=(0,1)) if palette else np.array([1,0,0])
            slot=int(np.argmax(weights))+1;palette_slot=slot
            paint=self.paints.get('Default'+str(slot),self.paints['Default1'])
        arr=self.image(source).copy() if source else np.ones((2,2,4),dtype=np.float32)
        arr[...,:3]*=color[:3]
        glass=bool(desc.get('translucent'))
        # Opaque source shaders ignore diffuseColor alpha (some rubber uses zero).
        if glass:arr[...,3]*=np.clip(color[3],0,1)
        else:arr[...,3]=1
        if perforated:arr[...,3]*=self.image(source)[...,0]
        rebaked=False
        if opacity or (perforated and groups):
            mask=self.image(opacity) if opacity else np.ones((2,2,4),dtype=np.float32)
            size=self.limit if target==2 else max(mask.shape[:2])
            if source and target!=duv:
                arr=reproject(arr,np.concatenate([g['uv'+str(duv)] for g in groups]),np.concatenate([g['uv'+str(target)] for g in groups]),size)
                rebaked=True
            else:arr=resized(arr,size)
            if target!=ouv:mask=on_uv(mask,ouv,target,size,groups)
            else:mask=resized(mask,size)
            # BeamNG's DDS is opacity, unlike the SDK's raw transparency stamp.
            arr[...,3]*=mask[...,0]
        # Preserve spatial paint-slot masks rather than tinting every pixel with
        # whichever slot has the highest average weight.
        palette_mixed=False
        if paint:
            palette_image=self.image(palette) if palette else np.full((2,2,4),[1,0,0,1],dtype=np.float32)
            size=max(*arr.shape[:2],*palette_image.shape[:2]);arr=resized(arr,size)
            weights=np.clip(on_uv(palette_image,int(stage.get('colorPaletteMapUseUV',0)),target,size,self.groups.get(name,[]))[...,:3],0,1)
            total=weights.sum(axis=2,keepdims=True);weights/=np.maximum(1,total)
            colors=np.array([self.paints.get('Default'+str(i),self.paints['Default1'])['baseColor'][:3] for i in (1,2,3)],dtype=np.float32)
            tint=weights@colors+np.maximum(0,1-weights.sum(axis=2,keepdims=True))
            arr[...,:3]*=tint
            palette_mixed=bool(np.max(np.ptp(weights,axis=(0,1)))>.005)
        ao_source=stage.get('ambientOcclusionMap',stage.get('aoMap'))
        if ao_source:
            arr=resized(arr,max(arr.shape[:2]))
            ao=on_uv(self.image(ao_source),int(stage.get('ambientOcclusionMapUseUV',stage.get('aoMapUseUV',0))),target,max(arr.shape[:2]),self.groups.get(name,[]))
            arr[...,:3]*=np.clip(ao[...,:1],0,1)
        normal_source=stage.get('normalMap');normal=None
        if normal_source and not glass:
            candidate=self.image(normal_source).copy()
            flat=float(np.max(np.ptp(candidate[...,:3],axis=(0,1))))<.005
            if int(stage.get('normalMapUseUV',0))==target or flat or self.groups.get(name):
                normal=candidate if flat else on_uv(candidate,int(stage.get('normalMapUseUV',0)),target,max(arr.shape[:2]),self.groups.get(name,[]))
                strength=float(stage.get('normalMapStrength',1))
                xy=(normal[...,:2]*2-1)*strength
                normal[...,:2]=(xy+1)/2
                normal[...,2]=(.5+.5*np.sqrt(np.maximum(0,1-np.sum(xy*xy,axis=2))))
                normal[...,3]=1
        shader='KSP/Alpha/Translucent Specular' if glass else 'KSP/Alpha/Cutoff Bumped' if cutout else 'KSP/Bumped Specular' if normal is not None else 'KSP/Specular'
        texname='material_'+str(len(self.materials));main_index=self.texture(arr,texname)
        if name in self.light_materials:
            from .lighting import emission_image
            emission=emission_image(arr,glass,float(np.clip(color[3],0,1)))
            light=self.light_materials[name]
            if light.get('emissive_map'):
                sampled=on_uv(self.image(light['emissive_map']),light['emissive_uv'],target,max(arr.shape[:2]),self.groups.get(name,[]))
                emission[...,:3]=sampled[...,:3]
            emission_name=texname+'_emission'
            image=Image.fromarray(np.clip(emission*255,0,255).astype('uint8'));image.thumbnail((1024,1024),Image.Resampling.LANCZOS);image.save(self.output/(emission_name+'.png'))
            self.light_materials[name]['texture']=emission_name
        rough=float(stage.get('roughnessFactor',.5));metal=float(stage.get('metallicFactor',0))
        if paint:rough*=paint.get('roughness',.35);metal*=paint.get('metallic',1)
        hardware=name in self.hardware_materials
        if hardware:
            rough=max(.6,rough)
            if normal is not None:
                normal[...,:2]=.5+(normal[...,:2]-.5)*.35
        spec_color=color[:3]*(np.array(paint['baseColor'][:3]) if paint else 1)
        spec=np.clip(.04*(1-metal)+np.sqrt(np.maximum(spec_color,0))*metal*.65,.02,.85)
        if hardware:spec=np.minimum(spec,.18)
        mat=self.mu.MuMaterial();mat.name=name;mat.shaderName=shader
        mat.colorProperties['_Color']=(1,1,1,1)
        if glass or not cutout:
            mat.colorProperties['_SpecColor']=tuple(float(v) for v in spec)+(1,)
            mat.floatProperties2['_Shininess']=max(.02,min(1,(1-rough)**3))
        if cutout and not glass:mat.floatProperties2['_Cutoff']=.45
        prop=self.mu.MuMatTex();prop.index=main_index;prop.scale=(1,1);prop.offset=(0,0);mat.textureProperties['_MainTex']=prop
        if normal is not None or (cutout and not glass):
            if normal is None:normal=np.full((2,2,4),[.5,.5,1,1],dtype=np.float32)
            normal_name=texname+'_normal'
            # PNG is conventional RGB. KSP's GetTexture(normal=true) packs R/G
            # into Unity's alpha/green normal format; do not pre-pack twice.
            normal_index=self.texture(normal,normal_name,1)
            prop=self.mu.MuMatTex();prop.index=normal_index;prop.scale=(1,1);prop.offset=(0,0);mat.textureProperties['_BumpMap']=prop
        pbr=None
        if not glass:
            size=max(arr.shape[:2]);rough_map=np.full((size,size),rough,dtype=np.float32);metal_map=np.full((size,size),metal,dtype=np.float32)
            for field,uvfield,destination in [('roughnessMap','roughnessMapUseUV',rough_map),('metallicMap','metallicMapUseUV',metal_map)]:
                if stage.get(field):
                    sampled=on_uv(self.image(stage[field]),int(stage.get(uvfield,0)),target,size,self.groups.get(name,[]))[...,0]
                    destination*=sampled
            coat=float(np.clip(float(stage.get('clearCoatFactor',0))*(paint.get('clearcoat',0) if paint else 1),0,1))
            if hardware:rough_map=np.maximum(rough_map,.6);coat=0
            pbr=bake_surface(self.output,texname,resized(arr,size),rough_map,metal_map,cutout,coat)
            if normal is not None:
                # Shabby's TEXTURE loader requests asNormalMap=false. Supply
                # DXT5nm-style GGGR explicitly instead of the stock RGB PNG.
                packed=normal.copy();packed[...,:3]=normal[...,1:2];packed[...,3]=normal[...,0]
                pbr['normal']=texname+'_pbr_normal'
                Image.fromarray(np.clip(packed*255,0,255).astype('uint8')).save(self.output/(pbr['normal']+'.png'))
        index=len(self.materials);self.materials.append(mat);self.cache[name]=(index,target)
        self.report.append({'material':name,'size':[arr.shape[1],arr.shape[0]],'shader':shader,'mesh_uv_set':target,'opacity_mask':opacity,'mixed_uv_baked':rebaked,'paint_slot':palette_slot,'spatial_palette_baked':palette_mixed,'ambient_occlusion_baked':bool(ao_source),'roughness':rough,'metallic':metal,'normal_map_imported':bool(normal_source and normal is not None),'normal_map_pending':bool(normal_source and normal is None)})
        self.report[-1]['pbr']=pbr
        self.report[-1]['hardware_finish_softened']=hardware
        self.report[-1]['grille_perforation_baked']=perforated
        self.report[-1]['dedicated_grille_atlas']=target==2
        if name in self.light_materials:self.report[-1]['lighting_emission']=self.light_materials[name]['texture']
        return index,target
