# SPDX-License-Identifier: MIT
param([string]$KspRoot='',[string]$OutputDirectory='',[string]$RuntimeDll='')
$ErrorActionPreference='Stop'
$taskRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$KspRoot){$KspRoot=Join-Path $taskRepo '../../KSP-ROD-Test'}
$taskManaged=Join-Path ([IO.Path]::GetFullPath($KspRoot)) 'KSP_x64_Data/Managed'
$taskOutput=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $taskRepo 'source/.build/ecu'}
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskReferences=@('Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.UIModule.dll','UnityEngine.UI.dll','UnityEngine.VehiclesModule.dll') | ForEach-Object {'/reference:'+(Join-Path $taskManaged $_)}
if(!$RuntimeDll){$RuntimeDll=Join-Path $taskRepo 'GameData/KDAVehicles/Plugins/KDAVehicles.dll'}
$taskReferences+=('/reference:'+([IO.Path]::GetFullPath($RuntimeDll)))
$taskReferences+=('/reference:'+(Join-Path ([IO.Path]::GetFullPath($KspRoot)) 'GameData/000_Harmony/0Harmony.dll'))
$taskSources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Where-Object Name -NotLike '*Tests*' | ForEach-Object FullName
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:library /optimize+ ('/out:'+(Join-Path $taskOutput 'KDAECU.dll')) $taskReferences $taskSources
if($LASTEXITCODE -ne 0){throw 'Independent ECU build failed'}
Write-Output ('Built standalone ECU: '+(Join-Path $taskOutput 'KDAECU.dll'))
