"""Translate selected JBeam light props/glow maps; never execute vehicle Lua."""
import copy,math,re
from pathlib import Path
import numpy as np
from PIL import Image
from .characteristics import resolve,numeric

FUNCTIONS={'lowbeam','highbeam','lowhighbeam','running','runninglights','brake','brakelights','reverse','signal_L','signal_R','hazard','ignitionLevel'}

def extract(parts,pc,vehicle):
    order,variables=resolve(parts,pc);nodes={};glows={};warnings=[];sources={}
    from .automation import relaxed_json
    for path in Path(vehicle).rglob('*.materials.json'):
        sources.update(relaxed_json(path.read_text(encoding='utf-8-sig')))
    for key,part in order:
        for row in part.get('nodes',[])[1:]:
            if not isinstance(row,list) or len(row)<4:continue
            try:nodes[row[0]]=np.array([numeric(v,variables) for v in row[1:4]])
            except ValueError:continue
        for name,entry in part.get('glowMap',{}).items():
            functions=entry.get('simpleFunction',{})
            if not isinstance(functions,dict):continue
            valid={k:float(v) for k,v in functions.items() if k in FUNCTIONS and isinstance(v,(int,float)) and math.isfinite(v) and v>0}
            if not valid:continue
            if entry.get('on') not in sources:
                warnings.append('Skipped glow '+name+': on-material is not included in the export.')
                continue
            on=sources[entry['on']].get('Stages',[{}])[0]
            color=on.get('emissiveFactor',[1,1,1])
            if not isinstance(color,list) or len(color)<3 or not all(isinstance(v,(int,float)) and math.isfinite(v) for v in color[:3]):color=[1,1,1]
            glows[name]={'material':name,'functions':valid,'emissive_rgb':[max(0,min(8,float(v))) for v in color[:3]],'source_on_material':entry.get('on',''),'emissive_map':on.get('emissiveMap'),'emissive_uv':int(on.get('emissiveMapUseUV',0))}
    emitters=[]
    for key,part in order:
        defaults={}
        for row in part.get('props',[])[1:]:
            if isinstance(row,dict):defaults.update(row);continue
            if not isinstance(row,list) or len(row)<12 or row[1] not in ('SPOTLIGHT','POINTLIGHT') or row[0] not in FUNCTIONS:continue
            try:
                ref,x,y=[nodes[n] for n in row[2:5]];dx=x-ref;dy=y-ref
                if np.linalg.norm(dx)<1e-7:raise ValueError('degenerate reference nodes')
                ex=dx/np.linalg.norm(dx);ey=dy-ex*np.dot(dy,ex)
                if np.linalg.norm(ey)<1e-7:raise ValueError('degenerate reference nodes')
                ey/=np.linalg.norm(ey);ez=np.cross(ex,ey)
                if not np.all(np.isfinite(ez)):raise ValueError('degenerate reference nodes')
                opts={**defaults,**(row[12] if len(row)>12 and isinstance(row[12],dict) else {})}
                base=opts.get('baseTranslation',{});position=ref+dx*numeric(base.get('x',0),variables)+dy*numeric(base.get('y',0),variables)+ez*numeric(base.get('z',0),variables)
                if 'baseTranslationGlobal' in opts:position=np.array([numeric(opts['baseTranslationGlobal'].get(a,0),variables) for a in 'xyz'])
                # Torque spotlights face local -Z. Props use intrinsic -X,-Z,+Y.
                angles=[numeric(row[5].get(a,0),variables)*math.pi/180 for a in 'xyz'];ax,ay,az=angles
                rx=np.array([[1,0,0],[0,math.cos(ax),math.sin(ax)],[0,-math.sin(ax),math.cos(ax)]])
                rz=np.array([[math.cos(az),math.sin(az),0],[-math.sin(az),math.cos(az),0],[0,0,1]])
                ry=np.array([[math.cos(ay),0,math.sin(ay)],[0,1,0],[-math.sin(ay),0,math.cos(ay)]])
                direction=np.column_stack((ex,ey,ez))@(rx@rz@ry)@np.array([0,0,-1.])
                if not np.all(np.isfinite(position)) or np.max(np.abs(position))>30:raise ValueError('invalid position')
                color=opts.get('lightColor',{'r':255,'g':255,'b':255})
                rgb=[max(0,min(1,numeric(color.get(a,255),variables)/255)) for a in 'rgb']
                intensity=numeric(opts.get('lightBrightness',1),variables)
                if 'lightIntensityCd' in opts:intensity=numeric(opts['lightIntensityCd'],variables)/5000
                if row[1]=='POINTLIGHT' and 'lightIntensityLm' in opts:intensity=numeric(opts['lightIntensityLm'],variables)/(1250/math.pi)
                emitters.append({'function':row[0],'kind':row[1],'position_beamng_m':position.tolist(),'direction_beamng':direction.tolist(),'color_rgb':rgb,'range_m':max(.1,min(150,numeric(opts.get('lightRange',10),variables))),'brightness':max(0,min(8,intensity)),'outer_angle_deg':max(5,min(160,numeric(opts.get('lightOuterAngle',45),variables))),'inner_angle_deg':max(0,min(160,numeric(opts.get('lightInnerAngle',0),variables))),'shadows':bool(opts.get('lightCastShadows',False)),'source_nodes':row[2:5]})
            except (KeyError,ValueError,TypeError,ZeroDivisionError) as e:warnings.append('Skipped unsupported light prop '+str(row[2])+': '+str(e))
    return {'version':1,'basis':'Selected JBeam props, reference-node frames and glowMap simpleFunction; source ranges/colors retained, Unity brightness approximated.','emitters':emitters,'materials':list(glows.values()),'warnings':warnings}

def emission_image(rgba,glass=False,opacity_factor=1):
    result=np.asarray(rgba,dtype=np.float32).copy()
    # A missing emissive map means an untextured source emission color.
    # Dark off-state lens albedo must not suppress the exported lit color.
    result[...,:3]=1
    result[...,3]=np.clip(result[...,3]/max(.001,opacity_factor if glass else 1),0,1)
    return result

def bind_existing(manifest,models,source_models=None):
    """Add masks to an existing car without rebuilding its meshes or physics."""
    lookup={r['material']:r for r in manifest['visual']['material_conversion']}
    source_models=Path(source_models or models)
    from .mesh import mu
    body=mu.Mu().read(str(source_models/'body.mu'))
    textures={m.name:body.textures[m.textureProperties['_MainTex'].index].name for m in body.materials if '_MainTex' in m.textureProperties}
    for item in manifest.get('lighting',{}).get('materials',[]):
        report=lookup.get(item['material'])
        if not report:continue
        stem='material_'+str(manifest['visual']['material_conversion'].index(report));path=source_models/(textures.get(item['material'],stem)+'.png')
        if not path.is_file():continue
        rgba=np.asarray(Image.open(path).convert('RGBA'),dtype=np.float32)/255
        glass='Translucent' in report['shader'];factor=float(np.max(rgba[...,3])) if glass else 1
        out=emission_image(rgba,glass,factor);name=stem+'_emission';image=Image.fromarray(np.clip(out*255,0,255).astype('uint8'));image.thumbnail((1024,1024),Image.Resampling.LANCZOS);image.save(Path(models)/(name+'.png'))
        item['texture']=name;report['lighting_emission']=name

def isolate_rear(model,manifest):
    """Separate rear lamp faces sharing a material with front headlights.

    Use axle geometry, never car IDs/material indices. Preserve artwork and
    signals; the runtime recognizes these names for red brake-only emission.
    """
    from .interior import subset
    data=manifest.get('lighting',{})
    rear=-sum(w['position_beamng_m'][1] for w in manifest['wheels'] if w['axle']=='rear')/2
    eligible={m['material'] for m in data.get('materials',[]) if set(m['functions'])&{'brake','brakelights','lowbeam','highbeam','lowhighbeam','running','runninglights'} and not set(m['functions'])&{'signal_L','signal_R','hazard'}}
    groups=[]
    def visit(parent,offset):
        offset=offset+np.asarray(parent.transform.localPosition)
        for obj in list(parent.children):
            mesh=getattr(obj,'shared_mesh',None)
            if mesh is not None and len(mesh.submeshes)==1 and len(obj.renderer.materials)==1 and model.materials[obj.renderer.materials[0]].name in eligible:
                v=np.asarray(mesh.verts)+offset+np.asarray(obj.transform.localPosition);tri=np.asarray(mesh.submeshes[0],dtype=int);mask=v[tri][:,:,2].mean(axis=1)<rear
                if mask.any():
                    name=obj.transform.name
                    if (~mask).any():
                        back=copy.deepcopy(obj);back.children=[];back.shared_mesh=subset(mesh,mask);obj.shared_mesh=subset(mesh,~mask);parent.children.append(back)
                    else:back=obj
                    back.transform.name='KDARearBrakeSkin_'+name
                    groups.append({'source_renderer':name,'rear_renderer':back.transform.name,'rear_triangles':int(mask.sum()),'front_triangles_retained':int((~mask).sum())})
            visit(obj,offset)
    visit(model.obj,np.zeros(3));data.update(rear_lamp_plane_body_y_m=rear,rear_brake_groups=groups,rear_brake_isolation_version=1)

def definitions(manifest):
    data=manifest.get('lighting',{});vid=manifest['id'];url='KDAVehicles/Vehicles/'+vid+'/Models/'
    vec=lambda v:','.join(format(0 if abs(float(x))<1e-12 else float(x),'.9g') for x in v)
    origin=np.array(manifest['origin_beamng_m']);text='KDA_VEHICLE_LIGHTS\n{\npart = '+manifest['runtime']['body_part']+'\nversion = 1\n'
    if data.get('rear_brake_isolation_version')==1:text+='rearLampPlane = '+format(data['rear_lamp_plane_body_y_m'],'.9g')+'\n'
    for item in data.get('emitters',[]):
        text+='LIGHT\n{\nfunction = '+item['function']+'\nkind = '+item['kind']+'\nposition = '+vec(-np.array(item['position_beamng_m'])+origin)+'\ndirection = '+vec(-np.array(item['direction_beamng']))+'\ncolor = '+vec(item['color_rgb'])+'\nrange = '+str(item['range_m'])+'\nbrightness = '+str(item['brightness'])+'\nangle = '+str(item['outer_angle_deg'])+'\ninnerAngle = '+str(item['inner_angle_deg'])+'\nshadows = '+str(item['shadows']).lower()+'\n}\n'
    for item in data.get('materials',[]):
        if not item.get('texture'):continue
        text+='SURFACE\n{\nmaterial = '+item['material']+'\ntexture = '+url+item['texture']+'\ncolor = '+vec(item['emissive_rgb'])+'\n'
        text+=''.join('signal = '+name+','+str(weight)+'\n' for name,weight in item['functions'].items())+'}\n'
    return text+'}\n'

def write(manifest,dest):
    (Path(dest)/'lighting.cfg').write_text(definitions(manifest),encoding='utf-8')
