"""Windows UAC helper protocol for installations under Program Files."""
import ctypes,json,os,subprocess,sys,tempfile,zipfile
from ctypes import wintypes
from pathlib import Path
from .automation import safe_extract
from .installer import install

def install_job(path):
    path=Path(path).resolve();job=json.loads(path.read_text(encoding='utf-8'))
    # Result and package are always siblings in this application's temp job.
    result=path.parent/'result.json';archive=path.parent/'package.zip'
    try:
        with tempfile.TemporaryDirectory(prefix='kda-install-') as temp:
            safe_extract(archive,temp);info=install(temp,job['ksp'],job.get('save'),log=lambda _:None)
        result.write_text(json.dumps({'ok':True,'result':info}),encoding='utf-8');return 0
    except Exception as error:
        result.write_text(json.dumps({'ok':False,'error':str(error)}),encoding='utf-8');return 1

def elevated_install(package,ksp,save=None):
    if not getattr(sys,'frozen',False):raise PermissionError('Administrator installation requires the packaged EXE. You can still convert without installing.')
    with tempfile.TemporaryDirectory(prefix='kda-install-job-') as temp:
        temp=Path(temp);job=temp/'job.json';job.write_text(json.dumps({'ksp':str(ksp),'save':save}),encoding='utf-8')
        with zipfile.ZipFile(temp/'package.zip','w',zipfile.ZIP_DEFLATED) as z:
            for p in Path(package).rglob('*'):
                if p.is_file():z.write(p,p.relative_to(package))
        class ExecuteInfo(ctypes.Structure):
            _fields_=[('cbSize',wintypes.DWORD),('fMask',wintypes.ULONG),('hwnd',wintypes.HWND),('lpVerb',wintypes.LPCWSTR),('lpFile',wintypes.LPCWSTR),('lpParameters',wintypes.LPCWSTR),('lpDirectory',wintypes.LPCWSTR),('nShow',ctypes.c_int),('hInstApp',wintypes.HINSTANCE),('lpIDList',ctypes.c_void_p),('lpClass',wintypes.LPCWSTR),('hkeyClass',wintypes.HKEY),('dwHotKey',wintypes.DWORD),('hIcon',wintypes.HANDLE),('hProcess',wintypes.HANDLE)]
        info=ExecuteInfo();info.cbSize=ctypes.sizeof(info);info.fMask=0x40;info.lpVerb='runas';info.lpFile=sys.executable;info.lpParameters=subprocess.list2cmdline(['--install-job',str(job)]);info.nShow=0
        shell=ctypes.WinDLL('shell32',use_last_error=True);shell.ShellExecuteExW.argtypes=[ctypes.POINTER(ExecuteInfo)]
        if not shell.ShellExecuteExW(ctypes.byref(info)):raise OSError('Administrator approval was cancelled or the installer could not start.')
        kernel=ctypes.WinDLL('kernel32',use_last_error=True);kernel.WaitForSingleObject.argtypes=[wintypes.HANDLE,wintypes.DWORD];kernel.CloseHandle.argtypes=[wintypes.HANDLE]
        try:
            while kernel.WaitForSingleObject(info.hProcess,1000)==258:pass
        finally:kernel.CloseHandle(info.hProcess)
        if not (temp/'result.json').is_file():raise OSError('The administrator installer did not return a result. The conversion package is still available.')
        reply=json.loads((temp/'result.json').read_text(encoding='utf-8'))
        if not reply['ok']:raise ValueError(reply['error'])
        return reply['result']
