"""Tip locations from terminal exhaust graph nodes, excluding engine inlets."""
from .characteristics import resolve,numeric
import math

def outlets(parts,pc):
    order,variables=resolve(parts,pc);result=[]
    for key,p in order:
        if p.get('slotType')!='Camso_exhaust':continue
        nodes={r[0]:[numeric(v,variables) for v in r[1:4]] for r in p.get('nodes',[])[1:] if isinstance(r,list) and len(r)>=4}
        edges={name:[] for name in nodes};inlets=set()
        props={}
        for row in p.get('beams',[])[1:]:
            if isinstance(row,dict):props.update(row);continue
            if not isinstance(row,list) or len(row)<2:continue
            opts={**props,**(row[2] if len(row)>2 and isinstance(row[2],dict) else {})}
            if not opts.get('isExhaust'):continue
            a,b=row[:2]
            if a in nodes and b in nodes:edges[a].append(b);edges[b].append(a)
            elif a in nodes:inlets.add(a)
            elif b in nodes:inlets.add(b)
        offset=[numeric(variables.get('$EngineOffset'+a,0),variables)+numeric(variables.get('$ExhaustOffset'+a,0),variables) for a in 'XYZ']
        for name,neighbours in edges.items():
            if len(neighbours)!=1 or name in inlets:continue
            direction=[nodes[name][i]-nodes[neighbours[0]][i] for i in range(3)];length=math.sqrt(sum(x*x for x in direction))
            if length<.001:continue
            result.append({'node':name,'position_beamng_m':[nodes[name][i]+offset[i] for i in range(3)],'direction_beamng':[x/length for x in direction],'basis':'Terminal exported exhaust beam graph'})
    return result
