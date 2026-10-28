"""Refresh the installable-file manifest of this standalone repository.

Run after copying explicitly rebuilt artifacts into GameData and Converter.
This does not copy from a developer's games or change player installations.
"""
from pathlib import Path
import hashlib
import json
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT / 'source/converter'))
from kdaconvert.version import VERSION, BUILD

def main():
    runtime = ROOT / 'GameData/KDAVehicles/Plugins/KDAVehicles.dll'
    converter = ROOT / 'Converter/KDAVehicleConverter.exe'
    if not runtime.is_file() or not converter.is_file():
        raise FileNotFoundError('Copy rebuilt KDA runtime and converter into the release first.')
    cars = sorted(p.name for p in (ROOT / 'GameData/KDAVehicles/Vehicles').iterdir() if p.is_dir())
    if cars != ['lizard_gt', 'rabbit_rally_x']:
        raise ValueError('This release must include Lizard GT and Rabbit Rally x.')
    entries = sorted(p.name for p in (ROOT / 'GameData').iterdir())
    if entries != ['KDAGarageBuilding','KDAVehicles']:
        raise ValueError('GameData must contain KDA only; install third-party mods separately.')
    if (ROOT / 'source/third_party').exists():
        raise ValueError('Do not embed external KSP mod source trees in this repository.')
    duplicate_dlls = [p for p in (ROOT / 'source').rglob('*.dll') if '.build' not in p.relative_to(ROOT).parts]
    if duplicate_dlls:
        raise ValueError('Installable DLLs belong only in top-level GameData; keep build output under .build.')
    dependencies = json.loads((ROOT / 'dependencies.json').read_text(encoding='utf-8'))
    framework = json.loads((runtime.parent.parent / 'framework.json').read_text(encoding='utf-8'))
    files = sorted(p for p in (ROOT / 'GameData').rglob('*') if p.is_file())
    files.append(converter)
    # .gitattributes publishes these text files with LF endings. Hash those
    # exact download bytes even when the Windows worktree contains CRLF.
    def published_bytes(path):
        data = path.read_bytes()
        return data.replace(b'\r\n', b'\n') if path.suffix.lower() in {'.cfg', '.json', '.md', '.txt', '.craft'} else data
    inventory = [
        {'path': p.relative_to(ROOT).as_posix(), 'bytes': len(published_bytes(p)),
         'sha256': hashlib.sha256(published_bytes(p)).hexdigest()}
        for p in files
    ]
    manifest = {
        'project': 'Kerbal Drift Agency', 'version': VERSION,
        'build': framework['build'], 'converter_build': BUILD, 'included_cars': ['Lizard GT', 'Rabbit Rally x (finished)'],
        'wheel_config_generator': 'kda.independent',
        'packaging': 'KDA-only; external KSP mods downloaded separately',
        'external_dependencies': dependencies['dependencies'], 'files': inventory,
    }
    (ROOT / 'release-manifest.json').write_text(
        json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(f'Manifest refreshed: {len(inventory)} installable files, Lizard GT and finished Rabbit Rally x.')

if __name__ == '__main__':
    main()

