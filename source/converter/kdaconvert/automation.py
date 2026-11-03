import json,re,hashlib,zipfile,math
from pathlib import Path

def validate_idle_rpm(value,limit):
    if isinstance(value,bool) or not isinstance(value,(int,float)) or not math.isfinite(value) or not 500<=value<=5000:
        raise ValueError('Unsupported idle RPM: '+str(value)+' (expected 500-5000)')
    if value>=limit:raise ValueError('Idle RPM must be below the engine RPM limit')
    return value

def relaxed_json(text):
    # Strip comments only outside strings; then trailing commas only outside strings.
    out=[];i=0;quoted=False;escape=False
    while i<len(text):
        c=text[i]
        if quoted:
            out.append(c)
            if escape:escape=False
            elif c=='\\':escape=True
            elif c=='"':quoted=False
            i+=1;continue
        if c=='"':quoted=True;out.append(c);i+=1
        elif text.startswith('//',i):
            end=text.find('\n',i);i=len(text) if end<0 else end
        elif text.startswith('/*',i):
            end=text.find('*/',i+2)
            if end<0:raise ValueError('Unterminated comment')
            i=end+2
        else:out.append(c);i+=1
    text=''.join(out);out=[];quoted=escape=False
    for i,c in enumerate(text):
        if quoted:
            out.append(c)
            if escape:escape=False
            elif c=='\\':escape=True
            elif c=='"':quoted=False
        elif c=='"':quoted=True;out.append(c)
        elif c==',' and text[i+1:].lstrip().startswith(('}',']')):continue
        else:out.append(c)
    cleaned=''.join(out)
    try:return json.loads(cleaned)
    except json.JSONDecodeError:
        # BeamNG also accepts omitted commas between complete values.
        pattern=re.compile(r'\s*("(?:[^"\\]|\\.)*"|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?|true|false|null|[{}\[\]:,])')
        tokens=[];pos=0
        while pos<len(cleaned):
            if not cleaned[pos:].strip():break
            m=pattern.match(cleaned,pos)
            if not m:raise ValueError('Unsupported JBeam token near '+cleaned[pos:pos+40])
            tokens.append(m.group(1));pos=m.end()
        cursor=0
        def parse():
            nonlocal cursor
            tok=tokens[cursor];cursor+=1
            if tok in ('{','['):
                obj={} if tok=='{' else [];close='}' if tok=='{' else ']'
                while tokens[cursor]!=close:
                    if tokens[cursor]==',':cursor+=1;continue
                    if tok=='{':
                        key=json.loads(tokens[cursor]);cursor+=1
                        if tokens[cursor]!=':':raise ValueError('Expected colon')
                        cursor+=1;obj[key]=parse()
                    else:obj.append(parse())
                cursor+=1;return obj
            return json.loads(tok)
        result=parse()
        while cursor<len(tokens) and tokens[cursor]==',':cursor+=1
        if cursor!=len(tokens):raise ValueError('Trailing JBeam data')
        return result

def safe_extract(source,dest):
    dest=Path(dest).resolve()
    with zipfile.ZipFile(source) as z:
        if sum(i.file_size for i in z.infolist())>600*1024**2:raise ValueError('Export exceeds 600 MiB unpacked limit')
        for i in z.infolist():
            if '\\' in i.filename or not (dest/i.filename).resolve().is_relative_to(dest):raise ValueError('Unsafe ZIP path')
            if (i.external_attr>>16)&0o170000==0o120000:raise ValueError('ZIP symlink not supported')
        z.extractall(dest)

def inspect(root,source_hash,config=None):
    root=Path(root);infos=list(root.glob('vehicles/*/info.json'))
    if len(infos)!=1:raise ValueError('First adapter requires exactly one vehicle per ZIP')
    vehicle=infos[0].parent
    if not re.fullmatch(r'[A-Za-z0-9_-]+',vehicle.name):raise ValueError('Vehicle directory must use a safe slug')
    info=relaxed_json(infos[0].read_text(encoding='utf-8-sig'));config=config or info['default_pc']
    info['Name']=re.sub(r'[\r\n{}=]', ' ',info['Name'])
    if not re.fullmatch(r'[A-Za-z0-9_-]+',config):raise ValueError('Invalid config ID')
    pc=relaxed_json((vehicle/(config+'.pc')).read_text(encoding='utf-8-sig'));meta=relaxed_json((vehicle/('info_'+config+'.json')).read_text(encoding='utf-8-sig'))
    parts={};parse_warnings=[]
    for p in sorted(vehicle.rglob('*.jbeam')):
        try:data=relaxed_json(p.read_text(encoding='utf-8-sig'))
        except (ValueError,IndexError) as e:
            parse_warnings.append('Skipped unsupported/malformed auxiliary JBeam '+p.name+': '+str(e));continue
        for k,v in data.items():parts[k]=(v,p)
    selected=pc['parts'];engine=parts[selected['Camso_Engine']][0]['mainEngine'];trans=parts[selected['Camso_Transmission']][0]
    table=engine['torque'];header=table[0];ti=header.index('throttle');ri=header.index('rpm');qi=header.index('torque')
    values=[r for r in table[1:] if isinstance(r,list) and isinstance(r[ti],(int,float))];full=max(r[ti] for r in values)
    friction=float(engine.get('friction',0));dynamic=float(engine.get('dynamicFriction',0))
    curve={float(r[ri]):max(0,float(r[qi])-friction-dynamic*float(r[ri])*3.141592653589793/30) for r in values if r[ti]==full}
    ratios={r[0]:r[4] for r in trans['variables'] if isinstance(r,list) and len(r)>4 and isinstance(r[0],str)}
    forward=[ratios[v] for v in trans['gearbox']['gearRatios'][2:]];reverse=ratios[trans['gearbox']['gearRatios'][0].replace('$=-','')]
    wheels=[]
    for axle,slot in [('front','Camso_wheels_front'),('rear','Camso_wheels_rear')]:
        part=parts[selected[slot]][0];settings={}
        for r in part['pressureWheels']:
            if isinstance(r,dict):settings.update(r)
        for r in part['flexbodies'][1:]:
            if not isinstance(r,list) or '_tyre_' not in r[0]:continue
            placement=r[3];pos=placement['pos'];groups=r[1];corner=next(g.split('_')[-1] for g in groups if g.startswith('tire_'))
            wheels.append({'corner':corner,'axle':axle,'position_beamng_m':[pos['x'],pos['y'],pos['z']],'radius_m':settings['radius'],'width_m':settings['tireWidth'],'tire_mesh':r[0],'rim_mesh':next(x[0] for x in part['flexbodies'][1:] if isinstance(x,list) and '_wheel_' in x[0]),'rotation_deg':[placement['rot'][a] for a in 'xyz']})
    if len(wheels)!=4:raise ValueError('Exactly four wheels required by first runtime adapter')
    peak=max(curve.items(),key=lambda p:p[1]);power=max((rpm*nm*3.141592653589793/30/1000,rpm) for rpm,nm in curve.items())
    manifest={'schema':'kda.vehicle','schema_version':1,'id':vehicle.name,'name':info['Name'],'source':{'adapter':'automation-beamng','sha256':source_hash,'exporter_version':info.get('ExporterVersion'),'config':config},'units':{'length':'m','mass':'kg','torque':'Nm','power':'kW','rpm':'rev/min'},'mass_kg':meta['Weight'],'drivetrain':meta['Drivetrain'],'engine':{'induction':'NA','idle_rpm':engine['idleRPM'],'limit_rpm':engine['maxRPM'],'torque_curve':[[r,t] for r,t in sorted(curve.items())],'curve_basis':'Full-throttle export torque minus constant and RPM-dependent export friction; no extra boost','peak_torque_nm':peak[1],'peak_power_kw':power[0]},'gearbox':{'type':'manual','ratios':forward,'reverse_ratio':reverse,'final_drive':pc.get('vars',{}).get('$finaldrive_r',pc.get('vars',{}).get('$finaldrive_f'))},'wheels':wheels,'warnings':['PBR materials are approximated with KSP shaders; deformation and BeamNG Lua are excluded.','Metadata top speed is not imported; gearing and tire radius determine theoretical speed.']}
    from .characteristics import enrich
    enrich(manifest,parts,pc,meta)
    from .engine_metadata import enrich_engine
    enrich_engine(manifest,vehicle)
    from .mass_distribution import enrich_mass
    enrich_mass(manifest,parts,pc)
    forward=manifest['gearbox']['ratios'];reverse=manifest['gearbox']['reverse_ratio']
    if not manifest['gearbox']['final_drive']:raise ValueError('Missing selected final-drive ratio')
    if manifest['drivetrain'] not in ('RWD','FWD','AWD'):raise ValueError('Unsupported drivetrain')
    if {w['corner'] for w in wheels}!={'FL','FR','RL','RR'}:raise ValueError('Expected unique FL/FR/RL/RR wheels')
    def number(value,low,high,label):
        if isinstance(value,bool) or not isinstance(value,(int,float)) or not math.isfinite(value) or not low<=value<=high:raise ValueError('Unsupported '+label+': '+str(value))
    number(manifest['mass_kg'],150,10000,'car mass')
    number(manifest['engine']['limit_rpm'],5500,20000,'RPM limit')
    validate_idle_rpm(manifest['engine']['idle_rpm'],manifest['engine']['limit_rpm'])
    number(manifest['gearbox']['final_drive'],1,8,'final drive')
    number(reverse,.2,6,'reverse ratio')
    if not 2<=len(forward)<=10:raise ValueError('Supported imported transmissions have 2-10 forward gears')
    for value in forward:number(value,.11,7.99,'gear ratio')
    if len(curve)<2:raise ValueError('Torque curve needs at least two points')
    for rpm,torque in curve.items():number(rpm,0,30000,'curve RPM');number(torque,0,10000,'torque')
    for w in wheels:
        number(w['radius_m'],.15,.8,'wheel radius');number(w['width_m'],.08,.8,'tire width')
        for value in w['position_beamng_m']:number(value,-10,10,'wheel position')
    manifest['warnings']+=parse_warnings
    visual=[]
    # Visual children such as exhaust and engine meshes are often default
    # nested slots, absent from the top-level .pc selection.
    from .characteristics import resolve
    active_parts={name for name,_ in resolve(parts,pc)[0]}
    for name,(part,file) in parts.items():
        if not (file.name==config+'_main.jbeam' or name in active_parts):continue
        for row in part.get('flexbodies',[])[1:]:
            if isinstance(row,list) and len(row)>3 and isinstance(row[3],dict):visual.append({'mesh':row[0],'node_groups':row[1],**row[3]})
    manifest['visual_bindings']=visual
    from .suspension_visual import anchors
    manifest['suspension_visual_anchors']=anchors(parts,pc)
    from .exhaust_visual import outlets
    manifest['exhaust_outlets']=outlets(parts,pc)
    from .lighting import extract as extract_lighting
    manifest['lighting']=extract_lighting(parts,pc,vehicle)
    manifest['warnings']+=manifest['lighting']['warnings']
    return manifest,vehicle

