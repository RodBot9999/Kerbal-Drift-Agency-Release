"""Numeric Automation export calibration. Never execute JBeam expressions or Lua."""
import ast,math,re

def numeric(value,variables):
    if isinstance(value,(int,float)) and not isinstance(value,bool): result=float(value)
    elif isinstance(value,str):
        text=value.removeprefix('$=')
        text=re.sub(r'\$([A-Za-z0-9_]+)',r'v_\1',text)
        def visit(n):
            if isinstance(n,ast.Constant) and isinstance(n.value,(int,float)):return float(n.value)
            if isinstance(n,ast.Name) and n.id.startswith('v_'):return variables.get('$'+n.id[2:])
            if isinstance(n,ast.BoolOp) and isinstance(n.op,ast.Or):
                for x in n.values:
                    v=visit(x)
                    # Lua treats zero as truthy.
                    if v is not None and v is not False:return v
                return None
            if isinstance(n,ast.UnaryOp) and isinstance(n.op,(ast.USub,ast.UAdd)):
                v=visit(n.operand);return -v if isinstance(n.op,ast.USub) else v
            if isinstance(n,ast.BinOp):
                a,b=visit(n.left),visit(n.right)
                if isinstance(n.op,ast.Add):return a+b
                if isinstance(n.op,ast.Sub):return a-b
                if isinstance(n.op,ast.Mult):return a*b
                if isinstance(n.op,ast.Div):return a/b
            raise ValueError('Unsupported numeric JBeam expression: '+str(value))
        try:result=visit(ast.parse(text,mode='eval').body)
        except (TypeError,SyntaxError,ZeroDivisionError) as e:raise ValueError('Unresolved numeric JBeam expression: '+value) from e
    else:raise ValueError('Expected numeric export value: '+str(value))
    if result is None or not math.isfinite(result):raise ValueError('Non-finite or missing export value: '+str(value))
    return float(result)

def resolve(parts,pc):
    selected=dict(pc['parts']);queue=list(selected.values());order=[];seen=set()
    while queue:
        key=queue.pop(0)
        if not key or key in seen:continue
        if key not in parts:continue # Optional stock BeamNG part absent from the ZIP.
        seen.add(key);p=parts[key][0];order.append((key,p))
        for row in p.get('slots',[])[1:]:
            if isinstance(row,list) and len(row)>1:queue.append(selected.setdefault(row[0],row[1]))
    variables={}
    for _,p in order:
        for row in p.get('variables',[])[1:]:
            if isinstance(row,list) and len(row)>4 and isinstance(row[4],(int,float)):variables[row[0]]=row[4]
    variables.update(pc.get('vars',{}));return order,variables

def interpolate(rows,rpm):
    if rpm<=rows[0][0]:return rows[0][1]
    for (a,x),(b,y) in zip(rows,rows[1:]):
        if rpm<=b:return x+(y-x)*(rpm-a)/(b-a)
    return rows[-1][1]

def induction_type(value):
    key=re.sub(r'[\s_-]','',str(value)).lower()
    kinds={'na':'NA','naturallyaspirated':'NA','turbo':'Turbo','turbocharged':'Turbo',
           'super':'Supercharged','supercharged':'Supercharged','supercharger':'Supercharged','twincharged':'TwinCharged'}
    if key not in kinds:raise ValueError('Unsupported induction: '+str(value))
    return kinds[key]

def enrich(m,parts,pc,meta):
    order,variables=resolve(parts,pc);enginepart=parts[pc['parts']['Camso_Engine']][0];engine=dict(enginepart['mainEngine'])
    for _,p in order:
        if 'torque' not in p.get('mainEngine',{}):engine.update(p.get('mainEngine',{}))
    table=engine['torque'];header=table[0];ti,ri,qi=[header.index(x) for x in ('throttle','rpm','torque')];pi=header.index('Pressure') if 'Pressure' in header else None
    rows=[r for r in table[1:] if isinstance(r,list) and isinstance(r[ti],(int,float))];full=max(r[ti] for r in rows)
    friction=engine.get('frictionTorque',[]);friction=sorted({float(r[0]):float(r[1]) for r in friction[1:] if isinstance(r,list)}.items())
    induction=induction_type(meta.get('Induction Type','NA'))
    boosted=induction!='NA'
    if boosted and pi is None:raise ValueError('Boosted export has no Pressure map; cannot safely separate boosted torque.')
    net={};pressure={};base={}
    for row in rows:
        if row[ti]!=full:continue
        rpm=float(row[ri]);loss=interpolate(friction,rpm) if friction else float(engine.get('friction',0))+float(engine.get('dynamicFriction',0))*rpm*math.pi/30
        torque=max(0,float(row[qi])-loss);absolute=float(row[pi]) if boosted else 1
        if not math.isfinite(absolute) or not .5<=absolute<=9:raise ValueError('Unsupported pressure map value')
        net[rpm]=torque;pressure[rpm]=max(0,absolute-1);base[rpm]=torque/absolute if boosted else torque
    e=m['engine'];e.update(induction=induction,name=enginepart.get('information',{}).get('name','Imported engine'),idle_rpm=numeric(engine['idleRPM'],variables),limit_rpm=numeric(engine.get('revLimiterRPM',engine['maxRPM']),variables),torque_curve=sorted(base.items()),full_load_torque_curve=sorted(net.items()),boost_curve_bar=sorted(pressure.items()),max_boost_bar=max(pressure.values()),curve_basis='Export full-load torque minus exported frictionTorque, divided by absolute Pressure for forced induction; dynamic boost reapplied once',peak_torque_nm=max(net.values()),peak_power_kw=max(r*t*math.pi/30000 for r,t in net.items()))
    audio_hint=engine.get('starterSample','')+' '+engine.get('instantAfterFireSound','')
    layout=re.search(r'(?:>|/)([vib]\d{1,2})(?:_|>)',audio_hint,re.I)
    e['layout']=layout.group(1).upper() if layout else 'Unknown';e['layout_basis']='Export engine audio identifier (fallback)'
    e['name']=re.sub(r'[\r\n{}=]',' ',e['name'])
    e['inertia_kg_m2']=float(engine.get('inertia',.1))
    turbo=next((p['turbocharger'] for _,p in order if 'turbocharger' in p),{})
    supercharger=next((p['supercharger'] for _,p in order if 'supercharger' in p),{})
    if induction=='TwinCharged' and (not turbo or not supercharger):
        raise ValueError('Twincharged export must include both selected turbocharger and supercharger data.')
    if supercharger:
        e['supercharger_export']={'type':str(supercharger.get('type','unknown')),
            **{k:numeric(supercharger[k],variables) for k in ['gearRatio','clutchEngageRPM','clutchDisengageRPM','maxPressure'] if k in supercharger}}
    sc=e.get('supercharger_export',{})
    e['mechanical_boost_max_bar']=sc.get('maxPressure',e['max_boost_bar']/.0689475729)*.0689475729
    e['supercharger_engage_rpm']=sc.get('clutchEngageRPM',0)
    e['supercharger_disengage_rpm']=sc.get('clutchDisengageRPM',0)
    if induction=='TwinCharged' and ('maxPressure' not in sc or sc['maxPressure']<=0):
        raise ValueError('Twincharged export lacks a positive supercharger pressure limit.')
    if not 0<=e['mechanical_boost_max_bar']<=8 or not 0<=e['supercharger_engage_rpm']<=1e9 or not 0<=e['supercharger_disengage_rpm']<=1e9:
        raise ValueError('Unsupported supercharger calibration')
    if turbo:
        e['turbo_export']={k:numeric(turbo[k],variables) for k in ['inertia','pressureRatePSI','turboSizeCoef','wastegateLimit'] if k in turbo}
    te=e.get('turbo_export',{})
    e['spool_seconds']=max(.15,min(2,(.25+.12*te.get('inertia',4))*max(.5,min(1.5,te.get('turboSizeCoef',.6)/.6))*30/max(10,te.get('pressureRatePSI',30))))
    e['spool_basis']='KDA response approximation from exported turbo inertia, size and pressure rate; export pressure map sets steady boost'
    if induction=='Supercharged':
        e['spool_seconds']=.08
        e['spool_basis']='Mechanical response using the exported combined pressure map; no turbo spool delay'
    elif induction=='TwinCharged':
        e['spool_basis']+='; mechanical boost is capped by exported supercharger pressure and fades at exported clutch disengagement RPM'
        m['warnings'].append('Twincharged transient response and smooth compressor handoff are KDA approximations; exported full-load torque/pressure are preserved without double boost.')
    trans=parts[pc['parts']['Camso_Transmission']][0]
    kind=next((r[0] for r in trans.get('powertrain',[])[1:] if isinstance(r,list) and 'Gearbox' in str(r[0])),None)
    type_map={'sequentialGearbox':'sequential','manualGearbox':'manual','automaticGearbox':'automatic','dctGearbox':'sequential'}
    if kind not in type_map:raise ValueError('Unsupported gearbox device: '+str(kind))
    g=m['gearbox'];g['type']=type_map[kind];g['ratios']=[numeric(v,variables) for v in trans['gearbox']['gearRatios'][2:]];g['reverse_ratio']=abs(numeric(trans['gearbox']['gearRatios'][0],variables))
    rear=1 if m['drivetrain']=='RWD' else 0 if m['drivetrain']=='FWD' else numeric(variables.get('$torquesplit',.5),variables)
    if not 0<=rear<=1:raise ValueError('Invalid rear torque split')
    m['drive']={'type':m['drivetrain'],'rear_torque_fraction':rear,'front_torque_fraction':1-rear,'front_final_drive':numeric(variables.get('$finaldrive_f',g['final_drive']),variables),'rear_final_drive':numeric(variables.get('$finaldrive_r',g['final_drive']),variables),'differentials':[{'part':key,'type':r[4].get('diffType')} for key,p in order for r in p.get('powertrain',[])[1:] if isinstance(r,list) and len(r)>4 and isinstance(r[4],dict) and r[0]=='differential']}
    from .driveline_metadata import enrich_active_drive
    enrich_active_drive(m,order,variables)
    g['final_drive']=m['drive']['rear_final_drive'] if rear else m['drive']['front_final_drive']
    trim=parts[pc['parts']['Camso_Trim']][0];a=trim.get('downforceModel',{});drag=trim.get('dragModel',{})
    for _,p in order:
        a={**a,**p.get('downforceModel',{})};drag={**drag,**p.get('dragModel',{})}
    m['aero']={'front_downforce_n':numeric(a.get('downforceTargetFront',0),variables),'rear_downforce_n':numeric(a.get('downforceTargetRear',0),variables),'reference_speed_kmh':numeric(a.get('targetSpeedKMH',200),variables),'reference_density_kg_m3':numeric(a.get('targetAirDensity',1.2041),variables),'drag_area_m2':numeric(drag.get('vehicleArea',0),variables),'side_drag_multiplier':numeric(drag.get('dragModX:',1.25),variables),'vertical_drag_multiplier':numeric(drag.get('dragModZ:',1.5),variables),'constant_front_n':numeric(a.get('constantForceFront',0),variables),'constant_rear_n':numeric(a.get('constantForceRear',0),variables),'source':'Selected JBeam cefaero model; speed-squared density scaling'}
    for key in ['front_downforce_n','rear_downforce_n','constant_front_n','constant_rear_n']:
        if not abs(m['aero'][key])<=100000:raise ValueError('Unsupported aero force')
    if not 10<=m['aero']['reference_speed_kmh']<=1000 or not .1<=m['aero']['reference_density_kg_m3']<=5 or not 0<=m['aero']['drag_area_m2']<=30:raise ValueError('Unsupported aerodynamic calibration')
    nodes={r[0]:r[1:4] for r in trim.get('nodes',[])[1:] if isinstance(r,list) and len(r)>3}
    groups=[r for r in trim.get('downforceNodeGroups',[])[1:] if isinstance(r,list)]
    for axle,flag in [('front',True),('rear',False)]:
        row=next((r for r in groups if r[2]==flag),None)
        if row and row[0] in nodes:m['aero'][axle+'_position_beamng_m']=[numeric(x,variables) for x in nodes[row[0]]]
    for w in m['wheels']:
        p=parts[pc['parts']['Camso_wheels_'+w['axle']]][0];s={}
        for row in p['pressureWheels']:
            if isinstance(row,dict):s.update(row)
        w['tire']={k:numeric(s.get(k,default),variables) for k,default in [('frictionCoef',1),('slidingFrictionCoef',1),('noLoadCoef',1.65),('fullLoadCoef',.15),('loadSensitivitySlope',.00015),('treadCoef',0)]}
        # KSP has one collider rather than loaded tread nodes. Preserve relative
        # compound/width differences with a deliberately mild width approximation.
        w['grip_calibration']=max(.5,min(1.6,w['tire']['frictionCoef']*w['tire']['noLoadCoef']/1.65*(w['width_m']/.235)**.15))
        w['drive_weight']=(1-rear if w['axle']=='front' else rear)/2
        w['drive_ratio']=m['drive'][w['axle']+'_final_drive']/g['final_drive']
    m['source']['physics_adapter_version']=2
    m['warnings']+=['Tire width/compound calibration is a KSP approximation, not BeamNG contact-patch physics.']
