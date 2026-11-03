"""Scoped, backed-up installation. Never installs a second framework DLL."""
import ctypes,hashlib,json,os,re,shutil,tempfile,uuid
from ctypes import wintypes
from datetime import datetime
from pathlib import Path
from .validation import validate_package
from .version import MIN_FRAMEWORK_BUILD

# Verified immediate predecessor; upgrades use the same rollback transaction.
COMPATIBLE_RUNTIME_HASHES={'60ad214dce2eb58ba07903da8791cb5f1ff43ee76cf6879be26795cc740ef4b8','f377fc272599775330aba77b2e2f7b6eb5b480d995f378b9ae087e23a53963ff','55dcd97c7b49f91b9e3a53d75ff2ada7364ed99c9d5b7266cb74dce95970ead4','9763296197ad61e253d5cdc4c7297ba93992c7ba49d1a61d948fe395297d8373','fa283614ad32e4d2d68ff5926951d1e6d86fbce73a46c6861f706950a21708ed','b34a23e850581948288d7cec09254e0cd0dd35997d06b1a30ac93faa488fb0ef','7405bdba7ec3381080ec948f5d1e450e56af9a95766618dc6a2f9feefbc7db12','429e43820c1a937488c9c91ceeefb1775816ba052f7968c0e25cc2b0035047f1','e6ed870fa1b03bb340882613e48d06efd3a85a38da6ea2920785ca565504bcc1','020184db9fc202d5c1029fdea1bcb354feff4ef6557d3e620ef73aaf68d15295','4684c5c302907d5ce1c41cfc3db9d932978371b22298c578fd0b4c9656e2ad94','483ce54b61c03af4f3ea6efd1035534152adedd02b90edd845ec9fe5a7bebb7f','87eb6d0ffb678471af83601aaeb740df23be848b930dbf948f2da15b68d8e020','d0bb887f29b8b7b3dd53055f9571c85f893af129822da168312b482ace80b6a9','ba3105d6148d57882aa58001f92557721c82ba02d8040f4ace2d4077e1ee2adf'}

COMPATIBLE_RUNTIME_HASHES.add('013f12aa527599819ddf4231efe4886ff147efdcd8ea3420d344828e22121c45')
COMPATIBLE_CREW_HASHES={'91a61740bf50969e15782066ed6cd3bd53c140bfa99a5cdee5a8e2f91962d846'}

# This predecessor shares the DLL filename but uses a different module ABI.
# It must never be upgraded while retaining its old car and crew configs.
INCOMPATIBLE_ABI_HASHES={'60ad214dce2eb58ba07903da8791cb5f1ff43ee76cf6879be26795cc740ef4b8'}
COMPATIBLE_RUNTIME_HASHES.difference_update(INCOMPATIBLE_ABI_HASHES)

# Verified runtimes installed during controller/telemetry development.
COMPATIBLE_RUNTIME_HASHES.update({
    'fbbe502712fa069487000f2186ad9463e4a41cc3b9c386d379b8007bd9f6dd48',
    '6315188924e4ed998b232ef2470dafc53691794708556f3fb9f47436e4485498',
    'a74b700e2c02c536ba7715032dd3ecddc8ac8671b363ac0e7965cba1db40d980',
    '00fc9568909fe9951cc48e7de314d6ac47d23fe67c6f4136ef7499b793c9b723',
    '14a154fa0a5d76d46d58eada4bf8cb8f626474a5a5e38c9a20de3e82fba365c1',
    '6e6907bc588946f9bb9db80c950786bd5d1f90431b76df8a3b52e777d23c4d4d',
    '72a8c792637b51e21cb9c290b302ea18fb4c88d1cf406a717e4abdebc4ee1078',
    '7badc65d406dbbcf094a6dccdb6c51d8b8db6a61a50d7778c53fcc6b88b6b424',
    'a43a29b62d77e986d0942dfcd8f1e606c6f071d668eb424b7a3b27f56460c847',
    'c0e0f373cd106b701c6852886393d57221d452d414c48c6474f1789e840b82f7',
    'c59c3295d6dfc36703d675f0b1bd1587e2c5ab297a897d401b6df7dd6da2732d',
    'd70958850a68a3e09d7e48303e225252583e85c2f0ff48facb7d69b4b957a0c2',
    'f377fc272599775330aba77b2e2f7b6eb5b480d995f378b9ae087e23a53963ff',
})

# Verified compatible fuel, recovery, smoke and lighting updates.
COMPATIBLE_RUNTIME_HASHES.update({'3dff6c29c1ae040a0c99e76114ab42dd1689d3a966b2200174710544bae30b06', 'e6f08af2be3271cad6b28469a111fb7e24673535c4d6c0fab5a64e7514d70cdf', 'dec5779bbc24e83875c6572ed77a207447dc8ce3a903447316bf441d62534c8e', '3534007602b2d1688ee36bf3ad93cf6fd781a3e0b8814439137c0ef52e89da6a', '7c2838d185df941d1fd3f10418a5aa87d8e27efec5f3bad463f8179195682ba4', '580178e1aafc71e20f3474b22f1a7a30a155e26a9e9201a7ae4d2b06904cce15'})

# Tested garage/framework release and its installed immediate predecessor.
COMPATIBLE_RUNTIME_HASHES.update({'c0e32b3b6b28cabb25a9dafb6405736417eee02bb6f28fdbe477e2cf4a698626','10d3377c80644a05e148db26681eeca78a20e0144ee269221dda5f338cc5e165'})
COMPATIBLE_RUNTIME_HASHES.update({'374585bb9c774aac308172e4fffd83ad61c4b7f00df19515de6f4dbe30eb1066','0704f991c5015d1855020f818385228a38eb831fdb0db513eb414a9e6b46548b','73ab22bac92bb9ae80c65192105287d54ef4eb909c68d6dcd41b0c3fed5924b3','4bd31d2e1b9da8767a1dcd243a1cb5bcd72d257c6468b3051251e72258dc4898','a4434fd9b1a0565c46ce25c23df5dd14b9d399067a4248fee623bf31d5eb48c2'})
COMPATIBLE_CREW_HASHES.update({'e545acb94173da6f9a86ef0844ec03753dae654a48cb12c33f7a861b32d8687e','a4c80022e608e5a55fa9cc73311a8946e62a8d8120dde1873688750c7a3f4861'})

def ksp_running():
    if os.name!='nt':return False
    class Entry(ctypes.Structure):
        _fields_=[('size',wintypes.DWORD),('usage',wintypes.DWORD),('pid',wintypes.DWORD),('heap',ctypes.c_size_t),('module',wintypes.DWORD),('threads',wintypes.DWORD),('parent',wintypes.DWORD),('priority',wintypes.LONG),('flags',wintypes.DWORD),('exe',wintypes.WCHAR*260)]
    kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.CreateToolhelp32Snapshot.restype=wintypes.HANDLE
    kernel.Process32FirstW.argtypes=[wintypes.HANDLE,ctypes.POINTER(Entry)];kernel.Process32NextW.argtypes=kernel.Process32FirstW.argtypes;kernel.CloseHandle.argtypes=[wintypes.HANDLE]
    handle=kernel.CreateToolhelp32Snapshot(2,0)
    if handle==ctypes.c_void_p(-1).value:raise OSError('Could not check whether KSP is running.')
    try:
        entry=Entry();entry.size=ctypes.sizeof(entry)
        if not kernel.Process32FirstW(handle,ctypes.byref(entry)):raise OSError('Could not inspect running applications.')
        while True:
            if entry.exe.lower() in ('ksp.exe','ksp_x64.exe'):return True
            if not kernel.Process32NextW(handle,ctypes.byref(entry)):break
        return False
    finally:kernel.CloseHandle(handle)

COMPATIBLE_RUNTIME_HASHES.update({'00b0bdae0d320fc82ce1e35d7fa673554bb0ff0a326f5d78a1babd67e817d98e', '2c6ba181377445def9b3f015e9c9cccf79a6766b33870642f06b175e07e6cac6', '68e5c96ba6d618a227a202c5f4acf03a258360745dcd8812434fcfb7c718b092', '08ba4c74e73b7e90b7d69bed8a87882d8b7250d3cd8194857709bd811e36e42a', '0e1c2b92f423b0111ab76fc4bda46cd06956870e0da6b26edb63023500603632', 'd9d54279f3fe76e4ddffa5591f080a2a21d14e773bb51b4a32f4ecccec29a8f9', '7232c47a504c04244397a90e00b8731cda9d5f1d01aa06f2c4f9d59b4599ad9b', 'c8f6074e87c54ad3564e29371e401486a1270a301d92e3d11d9487d1a77b14c8', '1e7bf7aa3e0ee59c5f13201f324b1f3a654dec32294b679cf030c6384e4bb320', '3c374316fd39f62710b20f34c38b54a753bb3f5a333cff2f1994b9294d2dc7a2', 'e9c49b21c62945250573f07a823fa128fd8a9b5159cbba0d2a8fd6092dfd0d6a', '73250925192f9558e034a3a5fa5a46a909a099496800d46ded91a5744d70999b'})

COMPATIBLE_RUNTIME_HASHES.add('27549dac571e1e4fc508b911694819c61e9a62aeb7bc5dffea9ec5df117d6607')

COMPATIBLE_RUNTIME_HASHES.add('d8d0f30f9392405745db097b4f2488e30ba69028f26bb3aa82b4269e239f0954')

def framework_stamp(root,runtime):
    """A stamp describes only the exact DLL it accompanies; mismatches fail closed."""
    try:
        data=json.loads((root/'framework.json').read_text())
        if data.get('abi')!='kda-standalone-1' or data.get('runtime_sha256')!=hashlib.sha256(runtime.read_bytes()).hexdigest():return None
        match=re.fullmatch(r'(\d{4})-(\d{2})-(\d{2})-kda(\d+)',data.get('build',''))
        return tuple(map(int,match.groups())) if match else None
    except (OSError,ValueError,TypeError):return None

def install(package,ksp,save=None,log=print):
    package=Path(package).resolve();ksp=Path(ksp).resolve();report=validate_package(package)
    if not (ksp/'KSP_x64.exe').is_file() and not (ksp/'KSP.exe').is_file():raise ValueError('Select the KSP installation folder containing KSP_x64.exe.')
    if ksp_running():raise ValueError('Close KSP before installing. Your converted package is ready; use Install last conversion after closing the game.')
    if save and (Path(save).name!=save or not (ksp/'saves'/save).is_dir()):raise ValueError('Select an existing KSP save, or leave the save selection empty.')
    vid=report['vehicle_id'];source=package/'GameData/KDAVehicles'
    manifest=json.loads((source/'Vehicles'/vid/'vehicle.json').read_text(encoding='utf-8'))
    ids={manifest['runtime']['body_part'],*manifest['runtime']['wheel_parts'].values()}
    for path in (ksp/'GameData/KDAVehicles/Vehicles').glob('*/vehicle.json'):
        if path.parent.name==vid:continue
        other=json.loads(path.read_text(encoding='utf-8'));runtime_ids=other.get('runtime',{})
        if ids&{runtime_ids.get('body_part'),*runtime_ids.get('wheel_parts',{}).values()}:raise ValueError('This car ID clashes with another installed car. Rename the car in Automation and export again.')
    existing=list((ksp/'GameData').rglob('KDAVehicles.dll'))
    # A standalone release cannot silently mix with a previous vehicle
    # framework whose saved module names differ. Recognize known predecessors
    # by content, without depending on their former branding or filenames.
    for candidate in (ksp/'GameData').rglob('*.dll'):
        if candidate.stat().st_size>4*1024*1024:continue
        candidate_hash=hashlib.sha256(candidate.read_bytes()).hexdigest()
        if candidate_hash in INCOMPATIBLE_ABI_HASHES or candidate not in existing and candidate_hash in COMPATIBLE_RUNTIME_HASHES:
            raise ValueError('A different vehicle framework is already installed. Use a clean KSP test copy, or migrate all vehicles before installing this standalone KDA release.')
    if len(existing)>1:raise ValueError('Multiple KDA runtimes found in GameData. Remove the duplicate before installation.')
    if not existing:raise ValueError('Install the KDA base GameData first. The car converter does not install the shared runtime or garage.')
    installed_stamp=framework_stamp(ksp/'GameData/KDAVehicles',existing[0])
    minimum=tuple(int(part) for part in re.fullmatch(r'(\d{4})-(\d{2})-(\d{2})-kda(\d+)',MIN_FRAMEWORK_BUILD).groups())
    if installed_stamp is None or installed_stamp<minimum:raise ValueError('The installed KDA framework is missing, modified, or older than '+MIN_FRAMEWORK_BUILD+'. Install the current base GameData separately before adding cars.')
    crew=ksp/'GameData/KDAVehicles/Plugins/KDACrew.dll'
    if not crew.is_file() or not (ksp/'GameData/KDAVehicles/Crew/seat.cfg').is_file():raise ValueError('The KDA base crew files are missing. Install the current base GameData separately before adding cars.')
    if manifest.get('audio',{}).get('type')=='kda.hybrid':
        from .hybrid_audio import verify_shared_bank
        verify_shared_bank(ksp/'GameData/KDAVehicles',manifest['audio']['family'])
    destinations=[(ksp/'GameData/KDAVehicles/Vehicles'/vid,source/'Vehicles'/vid)]
    craft=package/'Ships/SPH'/(vid+'.craft');craftname='KDA - '+vid+'.craft'
    craft_target=ksp/'saves'/save/'Ships/SPH'/craftname if save else ksp/'Ships/SPH'/craftname
    destinations.append((craft_target,craft))
    # A selected save already has access to stock Ships. Writing both created
    # duplicate load-menu entries; archive the old generated stock copy.
    stock_craft=ksp/'Ships/SPH'/craftname
    if save and stock_craft.exists():destinations.append((stock_craft,None))
    for target,_ in destinations:
        if not target.resolve().is_relative_to(ksp):raise ValueError('Install destination redirects outside the selected KSP folder.')
    backup=ksp/'KDAConverterBackups'/(datetime.now().strftime('%Y%m%d-%H%M%S')+'-'+uuid.uuid4().hex[:8]);backup.mkdir(parents=True)
    journal={'vehicle':vid,'destinations':[str(p.relative_to(ksp)) for p,_ in destinations],'state':'staging'}
    def record(state):journal['state']=state;(backup/'transaction.json').write_text(json.dumps(journal,indent=2),encoding='utf-8')
    record('staging');applied=[]
    try:
        with tempfile.TemporaryDirectory(prefix='.stage-',dir=backup) as temp:
            staged=[]
            for index,(target,item) in enumerate(destinations):
                if item is None:staged.append((target,None));continue
                ready=Path(temp)/str(index)
                if item.is_dir():shutil.copytree(item,ready)
                else:shutil.copy2(item,ready)
                staged.append((target,ready))
            if ksp_running():raise ValueError('KSP started during installation. Close it and retry.')
            record('installing')
            for target,ready in staged:
                target.parent.mkdir(parents=True,exist_ok=True);saved=backup/'previous'/target.relative_to(ksp)
                existed=target.exists();applied.append((target,saved,existed))
                if existed:saved.parent.mkdir(parents=True,exist_ok=True);os.replace(target,saved)
                if ready is not None:os.replace(ready,target)
            record('complete')
    except Exception:
        for target,saved,existed in reversed(applied):
            if saved.exists():
                if target.is_dir():shutil.rmtree(target)
                elif target.exists():target.unlink()
                os.replace(saved,target)
            elif not existed and target.exists():
                if target.is_dir():shutil.rmtree(target)
                else:target.unlink()
        record('rolled_back');raise
    log('Installed '+report['name']+'. Backup: '+str(backup))
    return {'vehicle':vid,'backup':str(backup),'craft':str(craft_target),'runtime_reused':True}



