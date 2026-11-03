param([string]$KspRoot='C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program',[string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskManaged=Join-Path $KspRoot 'KSP_x64_Data/Managed'
$taskCompiler='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskStage=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $taskRoot '.build/runtime/GameData'}
$taskDirectory=Join-Path $taskStage 'KDAVehicles/Plugins'
New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
$taskOutput=Join-Path $taskDirectory 'KDACrew.dll'
$taskReferences=@('Assembly-CSharp.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.AnimationModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.UI.dll') | ForEach-Object {'/reference:'+(Join-Path $taskManaged $_)}
$taskReferences+=('/reference:'+(Join-Path $KspRoot 'GameData/000_Harmony/0Harmony.dll'))
& $taskCompiler /nologo /optimize+ /target:library "/out:$taskOutput" $taskReferences (Join-Path $taskRoot 'plugin/KDACrew.cs') (Join-Path $taskRoot 'plugin/KDAInteriorShell.cs') (Join-Path $taskRoot 'plugin/KDACrewMass.cs')
if($LASTEXITCODE -ne 0){throw 'Crew compilation failed'}
