"""Original KDA Circuit seat, built from analytic surfaces and solid cushions.

Copyright (c) 2026 Roderic Systems. Generator code: GPL-3.0-only.
No downloaded geometry, UVs, textures or tracing is used. Dimensions and the
legacy installation origin are compatibility constraints, not geometry inputs.
"""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

# Metres, X right / Y up / Z forward. Preserve the installed seat's envelope.
MINIMUM = np.array([-.22858257591724396, -.02339235134422779, -.24421454966068268])
MAXIMUM = np.array([.22885271906852722, .7167928814888, .33298343420028687])
CREW_POSITION = [0., .045, .035]


def finish(name, vertices, triangles, material):
    """Crease normals at 48 degrees; planar UVs per face avoid collapsed charts."""
    v = np.asarray(vertices, dtype=float)
    f = np.asarray(triangles, dtype=int)
    cross = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    length = np.linalg.norm(cross, axis=1)
    assert np.all(length > 1e-12), name
    unit = cross / length[:, None]
    adjacency = [[] for _ in v]
    for i, face in enumerate(f):
        for vertex in face:
            adjacency[vertex].append(i)
    positions, normals, uv, result = [], [], [], []
    for i, face in enumerate(f):
        axes = np.argsort(np.abs(unit[i]))[-2::-1][:2]
        indices = []
        for vertex in face:
            incident = [j for j in adjacency[vertex] if unit[i] @ unit[j] > .66913]
            normal = cross[incident].sum(0)
            normal /= np.linalg.norm(normal)
            indices.append(len(positions))
            positions.append(v[vertex]); normals.append(normal)
            uv.append(tuple(v[vertex, axes] / .14))
        result.append(tuple(indices))
    return dict(name=name, positions=np.array(positions), normals=np.array(normals),
                uv=uv, triangles=result, material=material)


def panel(name, rows, columns, point, thickness, material, holes=()):
    """Analytic shell with true openings, a backing face and closed boundary walls."""
    vertices = np.array([point(r, c) for r in rows for c in columns])
    width = len(columns); triangles = []
    for r in range(len(rows) - 1):
        for c in range(width - 1):
            if any(r0 <= r < r1 and c0 <= c < c1 for r0, r1, c0, c1 in holes):
                continue
            a = r * width + c; b = a + 1; d = a + width; e = d + 1
            triangles.extend([(a, b, e), (a, e, d)])
    # Front orientation is supplied by the parametric grid.
    directed = [(a, b) for f in triangles for a, b in zip(f, f[1:] + f[:1])]
    counts = {}
    for a, b in directed:
        k = tuple(sorted((a, b))); counts[k] = counts.get(k, 0) + 1
    n = len(vertices); back = vertices - np.array(thickness)
    output = triangles + [(a+n, c+n, b+n) for a, b, c in triangles]
    for a, b in directed:
        if counts[tuple(sorted((a, b)))] == 1:
            output.extend([(b, a, a+n), (b, a+n, b+n)])
    return finish(name, np.vstack([vertices, back]), output, material)


def cushion(name, center, size, material=0, angle=0, bevel=.012):
    """Closed rounded rectangular cushion/bracket with inset bevel rings."""
    half = np.asarray(size) / 2; b = min(bevel, min(half)*.65)
    # Beveled XY outline, extruded along local Z with front/back bevels.
    x, y, z = half
    outline = [(-x+b,-y),(x-b,-y),(x,-y+b),(x,y-b),
               (x-b,y),(-x+b,y),(-x,y-b),(-x,-y+b)]
    verts = []
    for depth, inset in [(-z,b),(-z+b,0),(z-b,0),(z,b)]:
        for px, py in outline:
            verts.append((px*(x-inset)/x, py*(y-inset)/y, depth))
    faces = []
    for ring in range(3):
        for i in range(8):
            a=ring*8+i; b1=ring*8+(i+1)%8; c=b1+8; d=a+8
            faces.extend([(a,b1,c),(a,c,d)])
    faces += [(0,i+1,i) for i in range(1,7)]
    faces += [(24,24+i,24+i+1) for i in range(1,7)]
    a=np.radians(angle); rot=np.array([[1,0,0],[0,np.cos(a),-np.sin(a)],[0,np.sin(a),np.cos(a)]])
    return finish(name,np.array(verts)@rot.T+center,faces,material)


def build(dest, source_output=None):
    dest=Path(dest); dest.mkdir(parents=True,exist_ok=True)
    shapes=[]
    # A broad tapered shoulder arch, separate angular headrest and twin slots.
    ys=[.04,.14,.25,.36,.45,.49,.54,.58,.62,.66,.70,.7168]
    us=np.linspace(-1,1,13)
    def back(y,u):
        width=np.interp(y,[.04,.25,.45,.58,.62,.7168],[.155,.168,.216,.225,.16,.142])
        z=np.interp(y,[.04,.25,.45,.62,.7168],[-.06,-.11,-.16,-.207,-.222])
        return (u*width,y,z+.085*abs(u)**3)
    shapes.append(panel('KDA Circuit continuous arch shell',ys,us,back,(0,0,.014),2,
                        holes=[(5,7,2,4),(5,7,8,10)]))
    zs=np.linspace(-.12,.333,15)
    def pan(z,u):
        width=np.interp(z,[-.12,.06,.23,.333],[.153,.19,.183,.163])
        y=np.interp(z,[-.12,0,.20,.333],[-.003,.009,.029,.072])+.103*abs(u)**4
        return (u*width,y,z)
    # Increasing Z then X gives +Y normals.
    shapes.append(panel('KDA Circuit sculpted pan',zs,us[::-1],pan,(0,.019,0),2))
    for side in [-1,1]:
        shapes.append(cushion('KDA Circuit mounting rail '+str(side),[side*.122,.018,.08],[.032,.045,.32],3,bevel=.004))
        shapes.append(cushion('KDA Circuit hip pad '+str(side),[side*.169,.097,.073],[.055,.093,.34],0,angle=-4))
        shapes.append(cushion('KDA Circuit shoulder pad '+str(side),[side*.178,.421,-.06],[.064,.125,.045],0,angle=-14))
        shapes.append(cushion('KDA Circuit shoulder accent '+str(side),[side*.179,.422,-.034],[.009,.083,.005],1,angle=-14,bevel=.001))
        shapes.append(cushion('KDA Circuit back seam '+str(side),[side*.121,.281,-.074],[.004,.226,.006],1,angle=-13,bevel=.001))
    # Split pads form a centre channel and visible seams rather than a copied bucket.
    for side in [-1,1]:
        shapes.append(cushion('KDA Circuit thigh pad '+str(side),[side*.071,.043,.146],[.133,.055,.315],0,angle=8,bevel=.018))
        for i,(y,z,height) in enumerate([(.158,-.058,.124),(.29,-.095,.112),(.416,-.13,.096)]):
            shapes.append(cushion('KDA Circuit back pad '+str(side)+' '+str(i),[side*.061,y,z+.021],[.113,height,.032],0,angle=-13,bevel=.012))
    shapes.append(cushion('KDA Circuit head pad',[0,.659,-.15],[.243,.085,.038],0,angle=-9,bevel=.015))
    badge=cushion('KDA Circuit headrest badge',[0,.659,-.128],[.082,.024,.003],5,angle=-9,bevel=.0005)
    # Text projects only on the outward-facing badge face, avoiding atlas bleed.
    for i,p in enumerate(badge['positions']):
        if badge['normals'][i,2]>.97:
            badge['uv'][i]=(1-(p[0]+.041)/.082,(p[1]-(.659-.012))/.024)
    shapes.append(badge)
    # Match the full installation envelope; no transform is added to either MU root.
    vertices=np.vstack([s['positions'] for s in shapes]); oldmin=vertices.min(0); extent=np.ptp(vertices,axis=0)
    scale=(MAXIMUM-MINIMUM)/extent
    for s in shapes:
        s['positions']=(s['positions']-oldmin)*scale+MINIMUM
        s['normals']/=scale
        s['normals']/=np.linalg.norm(s['normals'],axis=1)[:,None]
    y,x=np.indices((512,512))
    weave=np.clip(241+3*np.sin(x*np.pi/2)+3*np.sin(y*np.pi/2)+2*np.sin((x+y)*.12),0,255).astype('uint8')
    Image.fromarray(np.repeat(weave[:,:,None],3,axis=2)).save(dest/'seat_fabric.png')
    Image.new('RGB',(4,4),(255,255,255)).save(dest/'seat_solid.png')
    image=Image.new('RGB',(512,160),(32,38,41));draw=ImageDraw.Draw(image)
    font=ImageFont.truetype('DejaVuSans-Bold.ttf',112)
    draw.text((256,74),'KDA',font=font,fill=(79,222,207),anchor='mm',stroke_width=0)
    image.save(dest/'kda_seat_badge.png')
    report={'design':'KDA Circuit','original_geometry':True,'source':'analytic KDA surfaces and cushions',
            'downloaded_geometry_or_textures_used':False,'triangles':sum(len(s['triangles']) for s in shapes),
            'bounds_min_m':MINIMUM.tolist(),'bounds_max_m':MAXIMUM.tolist(),
            'size_m':(MAXIMUM-MINIMUM).tolist(),'crew_local_position_m':CREW_POSITION,
            'same_attachment_origin':True,'true_shoulder_harness_openings':2,'texture_size_px':512}
    if source_output:
        output=Path(source_output);output.mkdir(parents=True,exist_ok=True)
        export_obj(shapes,output/'kda-circuit-seat.obj')
        for name in ['seat_fabric.png','seat_solid.png','kda_seat_badge.png']:
            (output/name).write_bytes((dest/name).read_bytes())
        (output/'geometry.json').write_text(json.dumps(report,indent=2)+'\n')
    return shapes,report


def export_obj(shapes,path):
    names={0:'fabric',1:'teal',2:'shell',3:'metal',5:'badge'}
    lines=['# Original KDA Circuit seat. Metres, +Y up, +Z forward.','mtllib kda-circuit-seat.mtl'];offset=1
    for s in shapes:
        lines+=['o '+s['name'].replace(' ','_'),'usemtl '+names[s['material']]]
        lines+=['v %.9f %.9f %.9f'%tuple(p) for p in s['positions']]
        lines+=['vt %.9f %.9f'%tuple(p) for p in s['uv']]
        lines+=['vn %.9f %.9f %.9f'%tuple(p) for p in s['normals']]
        lines+=['f '+' '.join(f'{i+offset}/{i+offset}/{i+offset}' for i in f) for f in s['triangles']]
        offset+=len(s['positions'])
    path.write_text('\n'.join(lines)+'\n')
    colors={0:(.18,.20,.22),1:(.08,.72,.65),2:(.10,.115,.13),3:(.24,.27,.30),5:(1,1,1)}
    path.with_suffix('.mtl').write_text('\n'.join('newmtl '+names[i]+'\nKd '+' '.join(map(str,c))+'\n'+('map_Kd kda_seat_badge.png\n' if i==5 else 'map_Kd seat_fabric.png\n' if i==0 else '') for i,c in colors.items()))
