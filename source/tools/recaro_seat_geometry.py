"""Build a game seat from the user's OBJ. No converter or physics dependencies.

Offline dependencies (workspace .build/seat-deps): fast-simplification 0.2.0,
xatlas 0.0.11. Source is retained under assets/seat-recaro/master.obj.
"""
from collections import Counter, defaultdict
from pathlib import Path
import hashlib
import json
import sys
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.build/seat-deps'))
import fast_simplification
import xatlas


def read_obj(path):
    vertices, triangles, groups, names = [], [], [], []
    group = -1
    for line in path.read_text().splitlines():
        fields = line.split()
        if not fields:
            continue
        if fields[0] == 'v':
            vertices.append([float(x) for x in fields[1:4]])
        elif fields[0] == 'o':
            group += 1
            names.append(fields[1])
        elif fields[0] == 'f':
            face = [int(x.split('/')[0]) - 1 for x in fields[1:]]
            for i in range(1, len(face)-1):
                triangles.append([face[0], face[i], face[i+1]])
                groups.append(group)
    return np.asarray(vertices), np.asarray(triangles), np.asarray(groups), names


def edges(faces):
    return Counter(tuple(sorted((int(a), int(b)))) for f in faces
                   for a, b in zip(f, np.roll(f, -1)))


def normals(vertices, faces):
    face = np.cross(vertices[faces[:, 1]]-vertices[faces[:, 0]],
                    vertices[faces[:, 2]]-vertices[faces[:, 0]])
    vertex = np.zeros_like(vertices)
    for corner in range(3):
        np.add.at(vertex, faces[:, corner], face)
    vertex /= np.maximum(np.linalg.norm(vertex, axis=1)[:, None], 1e-15)
    return vertex


def solidify(vertices, faces, thickness):
    """Offset a backing surface and bridge only its open edges, retaining holes."""
    count = len(vertices)
    directed = [(int(a), int(b)) for f in faces for a, b in zip(f, np.roll(f, -1))]
    counts = edges(faces)
    boundary = [(a,b) for a,b in directed if counts[tuple(sorted((a,b)))] == 1]
    if not boundary:
        return vertices, faces
    offset = vertices - normals(vertices, faces) * thickness
    back = faces[:, [0,2,1]] + count
    walls = [[b,a,a+count] for a,b in boundary] + [[b,a+count,b+count] for a,b in boundary]
    return np.vstack((vertices, offset)), np.vstack((faces, back, walls))


def smooth_split(vertices, faces, angle=48):
    """Smooth curves while keeping hard edges on brackets and thin side walls."""
    weighted = np.cross(vertices[faces[:,1]]-vertices[faces[:,0]],
                        vertices[faces[:,2]]-vertices[faces[:,0]])
    unit = weighted / np.linalg.norm(weighted,axis=1)[:,None]
    incident = defaultdict(list)
    for i, face in enumerate(faces):
        for vertex in face:
            incident[int(vertex)].append(i)
    verts, norms, result = [], [], np.empty_like(faces)
    threshold = np.cos(np.radians(angle))
    for vertex, adjacent in incident.items():
        clusters = []
        for face in adjacent:
            compatible = next((c for c in clusters if all(unit[face] @ unit[j] > threshold for j in c)), None)
            if compatible is None:
                clusters.append([face])
            else:
                compatible.append(face)
        for cluster in clusters:
            n = weighted[cluster].sum(axis=0)
            n /= np.linalg.norm(n)
            new = len(verts)
            verts.append(vertices[vertex]); norms.append(n)
            for face in cluster:
                result[face, np.where(faces[face] == vertex)[0][0]] = new
    return np.asarray(verts), result, np.asarray(norms)


def topology(vertices, faces):
    # Merge only UV/normal splits for topology checks.
    _, mapping = np.unique(np.round(vertices,6),axis=0,return_inverse=True)
    counts = edges(mapping[faces])
    assert all(x == 2 for x in counts.values()), 'Open or nonmanifold volume'
    assert all(len(set(f)) == 3 for f in mapping[faces]), 'Collapsed triangle'
    return {'boundary_edges':0,'nonmanifold_edges':0}


def build(dest):
    source = ROOT/'assets/seat-recaro/master.obj'
    vertices, faces, groups, names = read_obj(source)
    assert len(names)==5
    # Source X is longitudinal, Y vertical, Z lateral; preserve handedness.
    unity = vertices[:,[2,1,0]] * np.array([1,1,-1]) * .82
    unity[:,1] += .354
    # Seat bowl is under the existing pelvis, headrest behind the existing eye.
    unity[:,2] += .045
    labels = ['mounting brackets','front cushion','harness trim','upholstered bucket','rear shell']
    targets = [320,200,400,1250,350]
    material = [3,0,2,0,2]
    meshes, counts = [], []
    for i, target in enumerate(targets):
        ids = np.unique(faces[groups==i])
        lookup = np.full(len(vertices),-1); lookup[ids]=np.arange(len(ids))
        src = unity[ids]; original = lookup[faces[groups==i]]
        v, f = fast_simplification.simplify(src,original,target_count=target,agg=5,preserve_border=True)
        # Verify protected borders, dimensions and connected parts survive.
        before = edges(original); after = edges(f)
        assert sum(c==1 for c in before.values()) == sum(c==1 for c in after.values())
        bound_error = float(np.max(np.abs((v.max(0)-v.min(0))-(src.max(0)-src.min(0)))))
        assert bound_error < .006
        # Independent inner/outer tessellations can intersect across the source's
        # thin seam. Give the rigid rear shell separation before adding backing.
        if i==4:
            v = v + normals(v,f)*.009
        v, f = solidify(v,f,.0025 if i==3 else .002)
        topology(v,f)
        v,f,n = smooth_split(v,f,48 if i in (0,2) else 65)
        meshes.append((v,f,n))
        counts.append({'piece':labels[i],'source_triangles':len(original),'optimized_triangles':len(f),'bounds_error_m':bound_error})
    atlas = xatlas.Atlas()
    for v,f,n in meshes:
        atlas.add_mesh(v.astype('float32'),f.astype('uint32'),n.astype('float32'))
    packing=xatlas.PackOptions(); packing.resolution=1024; packing.padding=6
    atlas.generate(pack_options=packing)
    shapes=[]
    for i,(v,f,n) in enumerate(meshes):
        mapping, indices, uv = atlas[i]
        shape={'name':'KDA seat '+labels[i], 'positions':v[mapping], 'normals':n[mapping],
               'uv':list(map(tuple,uv)), 'triangles':list(map(tuple,indices)), 'material':material[i]}
        topology(shape['positions'],indices)
        assert np.isfinite(uv).all() and ((uv>=0)&(uv<=1)).all()
        assert np.ptp(uv,axis=0).min()>.01
        shapes.append(shape)
    # Restrained woven fabric: broad tonal variation and fine crossed threads.
    y,x=np.indices((1024,1024))
    shade=246+2*np.sin(x*.16)+2*np.sin(y*.16)+1.5*np.sin((x+y)*1.57)
    Image.fromarray(np.repeat(np.clip(shade,0,255)[:,:,None],3,axis=2).astype('uint8')).save(dest/'seat_fabric.png')
    report={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest().upper(),
            'source_triangles':len(faces),'optimized_triangles':sum(len(s['triangles']) for s in shapes),
            'pieces':counts,'UVs':'xatlas non-overlapping chart atlas, padded','texture_px':1024,
            'open_surfaces_thickened':True,'harness_holes_preserved':True,
            'normals_rebuilt':True,'scale':.82,'seat_pivot_unchanged':True,
            'rear_shell_outward_offset_m':.009,
            'original_file_modified':False,'converter_modified':False}
    assert 3000<=report['optimized_triangles']<=5000
    (ROOT/'docs/recaro-seat-build.json').write_text(json.dumps(report,indent=2)+'\n')
    export_obj(shapes,ROOT/'assets/seat-recaro/optimized-ksp.obj')
    return shapes,report


def export_obj(shapes,path):
    lines=['# KDA optimized Recaro-derived seat; Y-up, +Z forward, meters.',
           'mtllib optimized-ksp.mtl']
    offset=1
    for shape in shapes:
        lines += ['o '+shape['name'].replace(' ','_'),'usemtl '+{0:'fabric',2:'shell',3:'metal'}[shape['material']]]
        lines += ['v %.8f %.8f %.8f'%tuple(v) for v in shape['positions']]
        lines += ['vt %.8f %.8f'%tuple(v) for v in shape['uv']]
        lines += ['vn %.8f %.8f %.8f'%tuple(v) for v in shape['normals']]
        lines += ['f '+' '.join(f'{int(v)+offset}/{int(v)+offset}/{int(v)+offset}' for v in t) for t in shape['triangles']]
        offset+=len(shape['positions'])
    path.write_text('\n'.join(lines)+'\n')
    path.with_suffix('.mtl').write_text('newmtl fabric\nKd 0.19 0.19 0.19\nmap_Kd seat_fabric.png\n\nnewmtl shell\nKd 0.09 0.09 0.09\n\nnewmtl metal\nKd 0.34 0.37 0.40\n')
