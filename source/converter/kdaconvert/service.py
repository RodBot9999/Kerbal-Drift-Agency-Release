"""Local conversion API shared by the desktop app and command line."""
import hashlib,json,shutil,tempfile,re
from pathlib import Path
from .automation import safe_extract,inspect
from .mesh import export_models
from .ksp import write_ksp,write_audio,craft
from .validation import validate_package
from .version import VERSION,BUILD,MIN_FRAMEWORK_BUILD,FRAMEWORK_ABI

def convert(source,output,quality='balanced',seats=2,config=None,log=print,paint_color=None):
    source=Path(source);output=Path(output)
    if source.suffix.lower()!='.zip':raise ValueError('Select an Automation BeamNG ZIP export. Native .car files do not contain the required meshes.')
    if quality not in ('quality','balanced','lightweight') or not 0<=seats<=8:raise ValueError('Invalid texture quality or seat count.')
    if output.exists():raise ValueError('Conversion output already exists. Choose a new output folder.')
    if paint_color and not re.fullmatch(r'#[0-9a-fA-F]{6}',paint_color):raise ValueError('Body paint must be a #RRGGBB color')
    output.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='kda-source-') as tmp, tempfile.TemporaryDirectory(prefix='.kda-stage-',dir=output.parent) as stage:
        log('Reading export...');safe_extract(source,tmp)
        manifest,vehicle=inspect(tmp,hashlib.sha256(source.read_bytes()).hexdigest(),config);manifest['crew_capacity']=seats
        if paint_color:manifest['paint_color_override']=paint_color
        manifest['converter']={'version':VERSION,'build':BUILD,'required_framework':{'abi':FRAMEWORK_ABI,'min_build':MIN_FRAMEWORK_BUILD}}
        package=Path(stage)/'package';dest=package/'GameData/KDAVehicles/Vehicles'/manifest['id'];dest.mkdir(parents=True)
        log('Converting '+manifest['name']+' meshes and materials...');export_models(manifest,vehicle,dest/'Models',quality)
        log('Writing shared-framework parts, sound and assembled craft...');write_ksp(manifest,dest);write_audio(manifest,vehicle,dest)
        from .lighting import write as write_lighting
        write_lighting(manifest,dest)
        from .interior import write as write_interior
        write_interior(manifest,dest)
        crafts=package/'Ships/SPH';crafts.mkdir(parents=True);craft(manifest,crafts/(manifest['id']+'.craft'))
        manifest['crew_setup']='Attach a physical KDA race seat and doorway marker in the editor, then assign a Kerbal. The generated five-part craft is an uncrewed chassis.'
        shutil.copy2(vehicle/'license.txt',dest/'SOURCE-LICENSE.txt');(dest/'vehicle.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
        log('Validating meshes, texture references and part files...');report=validate_package(package)
        (package/'validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
        shutil.move(str(package),str(output))
    for warning in manifest['warnings']:log('Note: '+warning)
    log('Conversion ready: '+str(output));log(manifest['crew_setup']);return output
