"""Small cosmetic rig: exported JBeam fixed/hub nodes, never BeamNG physics."""
from .characteristics import resolve, numeric

def anchors(parts, pc):
    order, variables = resolve(parts, pc)
    selected = dict(pc['parts'])
    offsets = {}
    def visit(key, offset, path):
        if key not in parts or key in path:return
        offsets.setdefault(key, offset)
        for row in parts[key][0].get('slots', [])[1:]:
            if not isinstance(row, list) or len(row)<2:continue
            child=selected.get(row[0], row[1])
            opts=row[3] if len(row)>3 and isinstance(row[3],dict) else {}
            shift=opts.get('nodeOffset',{})
            delta=[numeric(shift.get(a,0),variables) for a in 'xyz']
            visit(child,[offset[i]+delta[i] for i in range(3)],path|{key})
    # The trim owns the suspension slot offsets. Visit it before standalone
    # .pc selections, which otherwise lose the inherited placement.
    visit(pc['parts'].get('Camso_Trim',''),[0,0,0],set())
    result={}
    for key,p in order:
        if p.get('slotType') not in ('Camso_suspension_F','Camso_suspension_R'):continue
        axle='F' if p['slotType'].endswith('_F') else 'R'
        offset=offsets.get(key)
        if offset is None:continue
        groups=[]
        for row in p.get('nodes',[])[1:]:
            if isinstance(row,dict) and 'group' in row:
                groups=row['group'];groups=[groups] if isinstance(groups,str) else groups
            elif isinstance(row,list) and len(row)>=4:
                kind='fixed' if 'subframe_'+axle in groups else 'moving' if 'hub_'+axle in groups else None
                if not kind:continue
                point=[numeric(row[i+1],variables)+offset[i] for i in range(3)]
                # Corner naming follows the exported tire groups: positive
                # BeamNG X is left (negative X in our KSP body frame).
                corner=axle+('L' if numeric(row[1],variables)>0 else 'R')
                result.setdefault(corner,{'fixed':[],'moving':[]})[kind].append(point)
    return {c:v for c,v in result.items() if v['fixed'] and v['moving']}
