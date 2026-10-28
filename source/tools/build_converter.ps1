param([string]$Python='python',[string]$OutputDirectory='release/KDAConverter',[string]$BuildDependencies='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=if([IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory}else{Join-Path $taskRoot $OutputDirectory}
$taskDependencies=if($BuildDependencies){$BuildDependencies}else{Join-Path $taskRoot '.build/converter-deps'}
$env:PYTHONPATH=$taskDependencies+';'+(Join-Path $taskRoot 'converter')
$env:PYINSTALLER_CONFIG_DIR=Join-Path $taskRoot '.build/pyinstaller-cache'
& $Python -m PyInstaller --noconfirm --clean --onefile --windowed --name KDAVehicleConverter --distpath $taskOutput --workpath (Join-Path $taskRoot '.build/pyinstaller') --specpath (Join-Path $taskRoot '.build') --paths (Join-Path $taskRoot 'converter') --paths (Join-Path $taskRoot 'tools/vendor/io_object_mu') --additional-hooks-dir (Join-Path $taskRoot 'tools/pyinstaller_hooks') --add-data ((Join-Path $taskRoot "converter/assets")+";converter/assets") (Join-Path $taskRoot 'converter/kda_converter_app.py')
if($LASTEXITCODE -ne 0){throw 'Converter build failed'}
