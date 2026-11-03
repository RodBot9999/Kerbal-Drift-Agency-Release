"""Read scalar engine metadata from Automation saves without executing Lua."""
import math,re

TOKEN=re.compile(r'--\[\[.*?\]\]|--[^\n]*|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|[A-Za-z_][A-Za-z_0-9]*|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?|[^\s]',re.S)

def engine_tables(text):
    if len(text)>16*1024*1024:raise ValueError('Embedded Automation save too large')
    tokens=[m.group() for m in TOKEN.finditer(text) if not m.group().startswith('--')]
    depth=0;tables={};i=0
    while i<len(tokens):
        token=tokens[i]
        if depth==1 and token in ('Family','Variant') and tokens[i+1:i+3]==['=','{']:
            name=token;values={};level=1;i+=3
            while i<len(tokens) and level:
                token=tokens[i]
                if level==1 and re.fullmatch(r'[A-Za-z_][A-Za-z_0-9]*',token) and tokens[i+1:i+2]==['='] and i+2<len(tokens):
                    value=tokens[i+2]
                    if value.startswith(('"',"'")):
                        values[token]=value[1:-1]
                    else:
                        try:
                            number=float(value)
                            if math.isfinite(number):values[token]=number
                        except ValueError:pass
                if token=='{':level+=1
                elif token=='}':level-=1
                i+=1
            tables[name]=values
            continue
        if token=='{':depth+=1
        elif token=='}':depth-=1
        i+=1
    return tables

def enrich_engine(manifest,vehicle):
    files=list(vehicle.glob('*.car'))
    if len(files)!=1:
        manifest['warnings'].append('Engine layout inferred from audio; a unique embedded Automation .car was unavailable.')
        return
    try:
        data=files[0].read_bytes()
        if len(data)>16*1024*1024:raise ValueError('Embedded Automation save too large')
        try:text=data.decode('utf-8-sig')
        except UnicodeError:text=data.decode('latin-1') # saves can contain raw binary Lua strings
        tables=engine_tables(text)
    except (ValueError,UnicodeError) as error:
        manifest['warnings'].append('Embedded engine metadata unreadable: '+str(error));return
    family=tables.get('Family',{});variant=tables.get('Variant',{});e=manifest['engine']
    layout=re.fullmatch(r'EngBlock_(V|Inl|I|Boxer|Box|B|F)(\d{1,2})_Name',str(family.get('BlockConfig','')),re.I)
    if layout:
        prefix={'v':'V','inl':'I','i':'I','boxer':'B','box':'B','b':'B','f':'B'}[layout.group(1).lower()]
        e['layout']=prefix+layout.group(2);e['layout_basis']='Embedded Automation .car Family.BlockConfig'
    capacity=variant.get('Capacity')
    if isinstance(capacity,(int,float)) and .05<=capacity<=30:e['displacement_l']=capacity
    for source,target in [('Bore','bore_mm'),('Stroke','stroke_mm')]:
        value=variant.get(source,family.get(source))
        if isinstance(value,(int,float)) and 10<=value<=300:e[target]=value
    e['automation_metadata']={'file':files[0].name,'family_name':family.get('Name',''),'variant_name':variant.get('Name',''),'aspiration_identifier':variant.get('AspirationType',''),'rpm_limit':variant.get('RPMLimit')}
    # BeamNG selected ECU and measured torque/pressure maps remain authoritative.
    if not layout:manifest['warnings'].append('Embedded save has no supported engine layout; audio inference retained.')
    if 'displacement_l' not in e:manifest['warnings'].append('Embedded save has no supported engine displacement.')
