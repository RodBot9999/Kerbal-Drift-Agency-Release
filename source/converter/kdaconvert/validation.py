"""Reject incomplete packages before they can reach a game installation."""
import json,math,re,wave
from pathlib import Path
from .mesh import mu
from .version import FRAMEWORK_ABI,MIN_FRAMEWORK_BUILD

def _read_material(self,m):
    self.name=m.read_string();self.shaderName=m.read_string()
    for _ in range(m.read_int()):
        name=m.read_string();kind=m.read_int()
        if kind==0:self.colorProperties[name]=m.read_float(4)
        elif kind==1:self.vectorProperties[name]=m.read_float(4)
        elif kind==2:self.floatProperties2[name]=m.read_float()
        elif kind==3:self.floatProperties3[name]=m.read_float()
        elif kind==4:self.textureProperties[name]=mu.MuMatTex().read(m)
        else:raise ValueError('Unknown MU material property')
    return self

def validate_package(root):
    root=Path(root).resolve();vehicles=root/'GameData/KDAVehicles/Vehicles'
    entries=list(vehicles.iterdir())
    if len(entries)!=1:raise ValueError('Package must contain exactly one converted vehicle.')
    vehicle=entries[0];m=json.loads((vehicle/'vehicle.json').read_text(encoding='utf-8'));vid=m['id']
    if not re.fullmatch(r'[A-Za-z0-9_-]+',vid) or vid!=vehicle.name:raise ValueError('Invalid vehicle ID.')
    if m['schema']!='kda.vehicle' or m['schema_version']!=1:raise ValueError('Unsupported vehicle package version.')
    if any(p.is_symlink() or not p.resolve().is_relative_to(root) for p in root.rglob('*')):raise ValueError('Package contains redirected paths.')
    required=m.get('converter',{}).get('required_framework')
    if required!={'abi':FRAMEWORK_ABI,'min_build':MIN_FRAMEWORK_BUILD}:raise ValueError('Unsupported required KDA framework version.')
    if (root/'GameData/KDAGarageBuilding').exists() or (root/'GameData/KDAVehicles/Plugins').exists() or (root/'GameData/KDAVehicles/Crew').exists() or (root/'GameData/KDAVehicles/framework.json').exists():raise ValueError('A converted car package must not contain the shared framework or garage.')
    required={'body.mu','FL.mu','FR.mu','RL.mu','RR.mu'}
    if {p.name for p in (vehicle/'Models').glob('*.mu')}!=required:raise ValueError('Missing body or corner-specific wheel model.')
    mu.read_material4=_read_material;triangles=0;body_objects=[]
    for path in sorted((vehicle/'Models').glob('*.mu')):
        model=mu.Mu();model.materials=[];model.textures=[]
        with path.open('rb') as stream:
            model.file=stream;model.magic,model.version=model.read_int(2)
            if model.magic!=mu.MuEnum.MODEL_BINARY or model.version!=mu.MuEnum.FILE_VERSION:raise ValueError('Invalid model header.')
            model.name=model.read_string();model.obj=mu.MuObject().read(model)
        del model.file
        for texture in model.textures:
            if Path(texture.name).name!=texture.name or not (path.parent/(texture.name+'.png')).is_file():raise ValueError('Missing model texture: '+texture.name)
        for mat in model.materials:
            if any(not 0<=v.index<len(model.textures) for v in mat.textureProperties.values()):raise ValueError('Invalid material texture index.')
        picks=[];objects=[]
        def walk(obj):
            nonlocal triangles
            objects.append(obj.transform.name)
            if obj.transform.name=='kdaSelectionCollider':
                collider=getattr(obj,'collider',None)
                if not isinstance(collider,mu.MuColliderBox) or collider.isTrigger or not all(math.isfinite(v) and v>0 for v in collider.size):raise ValueError('Invalid wheel selection collider')
                picks.append(obj)
            mesh=getattr(obj,'shared_mesh',None)
            if mesh:
                if not 0<len(mesh.verts)<=65535 or len(mesh.normals)!=len(mesh.verts) or len(mesh.uvs)!=len(mesh.verts):raise ValueError('Invalid mesh vertex attributes.')
                if any(not all(math.isfinite(x) for x in v) for v in mesh.verts):raise ValueError('Non-finite mesh coordinates.')
                if any(not 0<=i<len(model.materials) for i in obj.renderer.materials):raise ValueError('Invalid renderer material index.')
                for group in mesh.submeshes:
                    if any(min(t)<0 or max(t)>=len(mesh.verts) for t in group):raise ValueError('Invalid mesh triangle index.')
                    triangles+=len(group)
            for child in obj.children:walk(child)
        walk(model.obj)
        if path.stem=='body':body_objects=objects
        if path.stem=='body' and m['visual'].get('suspension_import_version',0)>=2:
            for item in m['visual'].get('suspension_hardware',[]):
                if not item['renderer_count'] or not any(n.startswith(item['mesh']+'_') for n in objects):raise ValueError('Missing imported suspension geometry: '+item['mesh'])
                attachment='KDASuspension_'+item['corner'] if item['animated'] else 'KDARigidSuspension_'+item['mesh']
                if attachment not in objects:raise ValueError('Missing suspension attachment: '+item['mesh'])
        if m['visual'].get('wheel_selection_colliders') and path.stem!='body' and len(picks)!=1:raise ValueError('Missing wheel selection collider')
    # Re-derive the canonical directory after recursive model traversal. Keep
    # package paths independent from deserialized model names and local readers.
    vehicle=root/'GameData/KDAVehicles/Vehicles'/vid
    parts=list((vehicle/'Parts').glob('*.cfg'))
    if len(parts)!=5:raise ValueError('Expected a body and four wheel part configs.')
    body=(vehicle/'Parts/body.cfg').read_text(encoding='utf-8')
    if m.get('interior',{}).get('version')==1:
        from .interior import definitions
        if 'name = KDAInterior' not in body:raise ValueError('Missing cockpit runtime module.')
        path=vehicle/'interior.cfg'
        if not path.is_file() or path.read_text(encoding='utf-8')!=definitions(m):raise ValueError('Cockpit definitions disagree with rig metadata.')
        if m['interior']['steering_rigged'] and m['interior']['steering_root'] not in body_objects:raise ValueError('Missing visual steering pivot.')
    for group in m.get('lighting',{}).get('rear_brake_groups',[]):
        if group['rear_renderer'] not in body_objects:raise ValueError('Missing isolated rear brake surface.')
    for module in ['KDAEngine','KDAController','KDASuspension']:
        if not any('name = '+module in p.read_text(encoding='utf-8') for p in parts):raise ValueError('Missing framework module: '+module)
    if m.get('mass_distribution',{}).get('valid'):
        from .mass_distribution import ksp_centers
        center=ksp_centers(m)[1]
        if 'name = KDAImportedMass' not in body or 'sourceValid = true' not in body:raise ValueError('Missing source mass calibration.')
        matches=re.findall(r'^CoMOffset\s*=\s*([^\n]+)',body,re.M)
        if len(matches)!=1:raise ValueError('Missing/duplicate chassis CG.')
        try:actual=[float(x.strip()) for x in matches[0].split(',')]
        except ValueError:raise ValueError('Invalid chassis CG.')
        if len(actual)!=3 or any(not math.isfinite(a) or abs(a-b)>1e-6 for a,b in zip(actual,center)):raise ValueError('Chassis CG disagrees with source mass distribution.')
    if not (root/'Ships/SPH'/(vid+'.craft')).is_file():raise ValueError('Missing assembled craft.')
    if m['audio'].get('type')=='kda.hybrid':
        from .hybrid_audio import metadata,config
        expected=metadata(m['engine'])
        if m['audio']!=expected:raise ValueError('Hybrid audio metadata disagrees with engine specifications.')
        if config(expected).strip() not in body:raise ValueError('Missing original shared hybrid audio configuration.')
        if m['audio'].get('exported_recordings_copied') is not False:raise ValueError('Hybrid package must not copy exported recordings.')
        if list((vehicle/'Sounds').glob('*.wav')):raise ValueError('Hybrid car packages must use shared audio, not per-car WAV files.')
    for name in m['audio'].get('effects',[]):
        if Path(name).name!=name:raise ValueError('Invalid sound effect reference')
        with wave.open(str(vehicle/'Sounds'/(name+'.wav')),'rb') as sound:
            if sound.getnchannels()!=1 or sound.getnframes()==0:raise ValueError('Invalid sound effect')
    for sample in m['audio']['samples']:
        for bank in ('off','on'):
            name=sample[bank]
            if Path(name).name!=name:raise ValueError('Invalid engine sound reference.')
            with wave.open(str(vehicle/'Sounds'/(name+'.wav')),'rb') as sound:
                if sound.getnchannels()!=1 or sound.getsampwidth()!=2 or sound.getframerate()!=22050 or sound.getnframes()==0:raise ValueError('Invalid engine sound format.')
    if not (vehicle/'SOURCE-LICENSE.txt').is_file():raise ValueError('Missing source asset license.')
    if m.get('lighting',{}).get('version')==1:
        from .lighting import definitions,FUNCTIONS
        if 'name = KDALighting' not in body:raise ValueError('Missing vehicle lighting module.')
        if not (vehicle/'lighting.cfg').is_file() or (vehicle/'lighting.cfg').read_text(encoding='utf-8')!=definitions(m):raise ValueError('Lighting definitions disagree with exported metadata.')
        for lamp in m['lighting'].get('emitters',[]):
            if lamp['function'] not in FUNCTIONS or not .1<=lamp['range_m']<=150:raise ValueError('Invalid exported light.')
            if any(not math.isfinite(v) for v in lamp['position_beamng_m']+lamp['direction_beamng']+lamp['color_rgb']):raise ValueError('Non-finite light geometry/color.')
        for item in m['lighting'].get('materials',[]):
            if not item.get('texture'):continue
            name=item['texture']
            if Path(name).name!=name or not (vehicle/'Models'/(name+'.png')).is_file():raise ValueError('Missing lighting emission mask: '+name)
    if m['visual'].get('optional_pbr_backend'):
        if not (vehicle/'pbr-materials.cfg').is_file():raise ValueError('Missing optional PBR definitions.')
        for item in m['visual']['material_conversion']:
            pbr=item.get('pbr')
            if not pbr:continue
            for key in ('main','metal','normal'):
                if key not in pbr:continue
                name=pbr[key]
                if Path(name).name!=name or not (vehicle/'Models'/(name+'.png')).is_file():raise ValueError('Missing optional PBR texture: '+name)
    return {'vehicle_id':vid,'name':m['name'],'parts':5,'models':5,'triangles':triangles,'fixtures':len(m['visual'].get('fixtures',[])),'crew_capacity':m.get('crew_capacity',0),'file_checks_passed':True,'in_game_test_required':True}
