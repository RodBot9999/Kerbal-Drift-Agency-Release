"""Conservative cosmetic rigging of merged Automation cockpit geometry.

Find a planar steering rim among connected mesh components; preserve every
triangle, material and UV when placing its nearby pieces under a visual pivot.
Ambiguous or unsupported geometry remains static rather than being guessed.
"""
import copy,math
from pathlib import Path
import numpy as np


def components(mesh):
    vertices=np.asarray(mesh.verts);triangles=np.asarray(mesh.submeshes[0],dtype=int)
    if not len(triangles):return []
    _,weld=np.unique(np.round(vertices,5),axis=0,return_inverse=True)
    parents=np.arange(weld.max()+1)
    def find(i):
        while i!=parents[i]:parents[i]=parents[parents[i]];i=parents[i]
        return i
    for a,b,c in weld[triangles]:
        a,b,c=find(a),find(b),find(c);parents[b]=a;parents[c]=a
    labels=np.asarray([find(i) for i in weld]);roots=labels[triangles[:,0]]
    return [np.unique(triangles[roots==i]) for i in np.unique(roots)]


def subset(mesh,mask,offset=None):
    result=copy.deepcopy(mesh);triangles=np.asarray(mesh.submeshes[0],dtype=int)[mask];used=np.unique(triangles)
    lookup=np.full(len(mesh.verts),-1,dtype=int);lookup[used]=np.arange(len(used))
    for field in ('verts','uvs','uv2s','normals','tangents','colors','boneWeights'):
        values=getattr(mesh,field,[])
        if len(values)==len(mesh.verts):setattr(result,field,[values[i] for i in used])
    if offset is not None:result.verts=[tuple(np.asarray(v)-offset) for v in result.verts]
    result.submeshes=[list(map(tuple,lookup[triangles]))]
    return result


def steering_candidate(records,front_y):
    candidates=[]
    for obj,groups in records:
        vertices=np.asarray(obj.shared_mesh.verts)
        for ids in groups:
            if len(ids)<100:continue
            v=vertices[ids];mean=v.mean(axis=0);_,s,axes=np.linalg.svd(v-mean,full_matrices=False)
            if s[0]<1e-6 or s[1]/s[0]<.65 or s[2]/s[1]>.35:continue
            axis=axes[2]
            if abs(axis[2])<.65:continue
            q=(v-mean)@axes[:2].T
            c=np.linalg.lstsq(np.column_stack((2*q,np.ones(len(q)))),(q*q).sum(axis=1),rcond=None)[0]
            pivot=mean+c[:2]@axes[:2];radial=np.linalg.norm((v-pivot)@axes[:2].T,axis=1);radius=float(np.median(radial))
            # MU coordinates are body X, body Z, body Y. A steering rim is
            # above the hubs, inside the cabin and behind the front axle.
            if not (.11<radius<.27 and -1.0<pivot[1]<-.10 and abs(pivot[0])<1.2 and .2<front_y-pivot[2]<2.5):continue
            error=float(np.median(abs(radial-radius))/radius)
            if error>.16 or np.quantile(radial,.1)<radius*.60:continue
            angles=np.arctan2(q[:,1]-c[1],q[:,0]-c[0]);bins=np.unique(np.floor((angles+math.pi)/(2*math.pi)*12).astype(int))
            if len(bins)<10:continue
            candidates.append((len(ids)*(1-error),pivot,axis,radius,float(np.quantile(radial,.99))))
    if not candidates:return None
    candidates.sort(key=lambda x:x[0],reverse=True);best=candidates[0]
    if any(np.linalg.norm(c[1]-best[1])>.12 and c[0]>best[0]*.65 for c in candidates[1:]):return None
    return best


def rig(model,manifest):
    from .mesh import node
    records=[]
    # Generated shell groups have identity local transforms; fixtures and
    # suspension are separate hierarchies and never selected as cockpit parts.
    for obj in model.obj.children:
        mesh=getattr(obj,'shared_mesh',None)
        if mesh is not None and len(mesh.submeshes)==1 and np.allclose(obj.transform.localPosition,0):records.append((obj,components(mesh)))
    front_y=-sum(w['position_beamng_m'][1] for w in manifest['wheels'] if w['axle']=='front')/2
    candidate=steering_candidate(records,front_y)
    report={'version':1,'steering_rigged':False,'basis':'Conservative planar-rim detection and connected-component isolation; ambiguous artwork remains static','dashboard_basis':'Runtime recognizes supported abscreen/digidash screen materials'}
    if candidate is None:
        manifest['warnings'].append('No unambiguous steering-wheel rim found; cockpit screen support remains available, steering artwork stays static.')
        return report
    _,pivot,axis,radius,outer=candidate
    if axis[2]>0:axis=-axis
    wheel=node('KDAInteriorSteering');wheel.transform.localPosition=tuple(pivot);moved=0;groups_moved=0
    for obj,groups in records:
        mesh=obj.shared_mesh;v=np.asarray(mesh.verts);chosen=[]
        for ids in groups:
            local=v[ids]-pivot;depth=local@axis;radial=np.linalg.norm(local-depth[:,None]*axis,axis=1)
            if radial.max()<=outer*1.12 and depth.min()>=-.125 and depth.max()<=.095:chosen.extend(ids)
        if not chosen:continue
        triangle=np.asarray(mesh.submeshes[0],dtype=int);selected=np.isin(triangle,chosen)
        if not np.all(selected.all(axis=1)|(~selected).all(axis=1)):raise ValueError('Cockpit split crossed a connected triangle')
        mask=selected.all(axis=1)
        skin=node('KDAWheelSkin_'+str(groups_moved));skin.shared_mesh=subset(mesh,mask,pivot);skin.renderer=copy.deepcopy(obj.renderer);wheel.children.append(skin);moved+=int(mask.sum());groups_moved+=1
        if (~mask).any():obj.shared_mesh=subset(mesh,~mask)
        else:model.obj.children.remove(obj)
    if moved:
        model.obj.children.append(wheel)
        report.update(steering_rigged=True,pivot_body_local_m=pivot[[0,2,1]].tolist(),axis_body_local=axis[[0,2,1]].tolist(),radius_m=radius,steering_root='KDAInteriorSteering',steering_lock_degrees=450,groups=groups_moved,triangles=moved)
    return report


def definitions(manifest):
    data=manifest['interior'];text='KDA_VEHICLE_INTERIOR\n{\npart = '+manifest['runtime']['body_part']+'\n'
    if data['steering_rigged']:
        text+='steeringRoot = '+data['steering_root']+'\nsteeringAxis = '+','.join(format(x,'.9g') for x in data['axis_body_local'])+'\nsteeringLock = 450\n'
    return text+'}\n'


def write(manifest,dest):
    (Path(dest)/'interior.cfg').write_text(definitions(manifest),encoding='utf-8')
