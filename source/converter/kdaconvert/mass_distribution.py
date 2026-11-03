"""Selected JBeam mass moments; safe numeric parsing, no Lua execution.

Pressure wheel rings are represented by their mass-weighted axle center.
This is an exported reference-pose estimate, not BeamNG runtime telemetry.
"""
import math
from .characteristics import numeric,resolve

def _number(value,variables):
    return numeric(value,variables)

def reconstruct(manifest,parts,pc):
    order,variables=resolve(parts,pc);nodes={};errors=[];explicit=0
    for name,part in order:
        if any(k in part for k in ('nodeOffset','nodeMove','nodeRotate','scaleNodes')):
            errors.append('Unsupported node transform in '+name)
        try:scale=_number(part.get('scalenodeWeight',1),variables)
        except ValueError:errors.append('Unresolved node weight scale in '+name);scale=1
        table=part.get('nodes',[]);settings={}
        if not table:continue
        header=table[0]
        try:indices=[header.index(k) for k in ('id','posX','posY','posZ')]
        except (ValueError,AttributeError):errors.append('Unsupported node header in '+name);continue
        for row in table[1:]:
            if isinstance(row,dict):settings.update(row);continue
            if not isinstance(row,list) or len(row)<len(header):
                errors.append("Malformed node row in "+name);continue
            opts={**settings,**(row[len(header)] if len(row)>len(header) and isinstance(row[len(header)],dict) else {})}
            try:
                key=row[indices[0]];mass=_number(opts['nodeWeight'],variables)*scale
                pos=[_number(row[i],variables) for i in indices[1:]]
                if mass<=0 or not isinstance(key,str):raise ValueError('Invalid node mass/id')
                if any(k in opts for k in ('nodeOffset','nodeMove','nodeRotate')):raise ValueError('Unsupported node transform')
                if key in nodes:raise ValueError('Duplicate node '+key)
                nodes[key]=(mass,pos);explicit+=1
            except (KeyError,ValueError,IndexError,TypeError) as e:errors.append(name+': '+str(e))
    points=list(nodes.values());generated=0
    for wheel in manifest['wheels']:
        key=pc.get('parts',{}).get('Camso_wheels_'+wheel['axle']);part=parts.get(key,({},None))[0]
        settings={}
        for row in part.get('pressureWheels',[])[1:]:
            if isinstance(row,dict):settings.update(row)
        definition=None
        for _,selected in order:
            table=selected.get('pressureWheels',[])
            if not table:continue
            for row in table[1:]:
                if isinstance(row,list) and row and row[0]==wheel['corner']:
                    definition=(table[0],row)
        try:
            # Ring mass per ray: two hub nodes and two tire nodes.
            rays=_number(settings['numRays'],variables)
            if rays<4 or rays>256 or int(rays)!=rays or int(rays)%2:raise ValueError('Unsupported wheel ray count')
            if any(k in settings for k in ('hubMass','tireMass','hubWeightGainRatio','offsetFromNode')):raise ValueError('Unsupported generated wheel mass/offset override')
            hub=_number(settings['hubWeight'],variables) if 'hubWeight' in settings else 2*rays*_number(settings['hubNodeWeight'],variables)
            tire=(_number(settings['tireWeight'],variables) if 'tireWeight' in settings else 2*rays*_number(settings['nodeWeight'],variables)) if settings.get('hasTire',True) else 0
            if settings.get('enableHubcaps',False):raise ValueError('Hubcap generator mass not supported')
            offset=_number(settings.get('wheelOffset',0),variables)
            if abs(offset)>1e-8:raise ValueError('Nonzero generated wheel offset not supported')
            pos=wheel['position_beamng_m']
            if not definition:raise ValueError('Missing selected pressure-wheel axle definition')
            if definition:
                header,row=definition;opts=row[len(header)] if len(row)>len(header) and isinstance(row[len(header)],dict) else {}
                if any(k in opts for k in ('nodeWeight','hubNodeWeight','hubWeight','tireWeight','hubWeightGainRatio','numRays','hasTire','wheelOffset','offsetFromNode')):raise ValueError('Wheel-row mass override not supported')
                a=nodes[row[header.index('node1:')]][1];b=nodes[row[header.index('node2:')]][1]
                center=[(a[i]+b[i])*.5 for i in range(3)]
                if math.dist(center,pos)>.05:raise ValueError('Wheel axle/visual centers disagree')
                pos=center
            if hub<=0 or tire<0:raise ValueError('Invalid wheel generated mass')
            points.append((hub+tire,pos));generated+=hub+tire
        except (KeyError,ValueError,TypeError,IndexError) as e:errors.append(wheel['corner']+': '+str(e))
    total=sum(m for m,_ in points);reference=float(manifest['mass_kg']);error=abs(total/reference-1) if reference>0 else 1
    if total<=0 or error>.08:errors.append('Reconstructed/metadata mass mismatch exceeds 8%')
    result={'basis':'Selected explicit JBeam node weights plus pressure-wheel ring masses at axle centers; reference-pose estimate','reference_mass_kg':reference,'reconstructed_mass_kg':total,'explicit_node_count':explicit,'generated_wheel_mass_kg':generated,'mass_error_fraction':error,'valid':not errors,'issues':errors}
    if not errors:
        cg=[sum(m*p[i] for m,p in points)/total for i in range(3)]
        front=sum(w['position_beamng_m'][1] for w in manifest['wheels'] if w['axle']=='front')/2
        rear=sum(w['position_beamng_m'][1] for w in manifest['wheels'] if w['axle']=='rear')/2
        fraction=(rear-cg[1])/(rear-front)
        height=cg[2]-sum(w['position_beamng_m'][2]-w['radius_m'] for w in manifest['wheels'])/4
        if not .1<=fraction<=.9 or not .08<=height<=1.5:
            result['valid']=False;result['issues'].append('Reconstructed CG outside supported vehicle bounds')
        else:result.update(center_beamng_m=cg,front_mass_fraction=fraction,height_above_reference_tire_plane_m=height)
    return result

def enrich_mass(manifest,parts,pc):
    data=reconstruct(manifest,parts,pc);manifest['mass_distribution']=data
    if not data['valid']:manifest['warnings'].append('Source mass distribution unavailable; retaining labelled generic CG fallback: '+'; '.join(data['issues'][:4]))
    else:manifest['warnings'].append('Source CG reconstructed from selected node/wheel masses (reference-pose estimate), front weight '+format(data['front_mass_fraction']*100,'.1f')+'%. KDA seats and seated crew add no vehicle mass.')

def ksp_centers(manifest):
    data=manifest.get('mass_distribution',{})
    if not data.get('valid'):return None
    origin=manifest.get('origin_beamng_m',[0,0,0]);target=[-x for x in data['center_beamng_m']];target[2]+=origin[2]
    # Native fuel shares the body's CoM. Solve the body including full fuel,
    # with the four existing wheel masses kept explicit rather than counted twice.
    mass=manifest['mass_kg'];wheelmass=sum(w.get('mass_kg',11) for w in manifest['wheels']);moment=[0.,0.,0.]
    for w in manifest['wheels']:
        p=[-x for x in w['position_beamng_m']];p[2]+=origin[2]
        for i in range(3):moment[i]+=w.get('mass_kg',11)*p[i]
    if mass-wheelmass<=0:raise ValueError('Invalid chassis mass budget')
    body=[(mass*target[i]-moment[i])/(mass-wheelmass) for i in range(3)]
    return target,body
