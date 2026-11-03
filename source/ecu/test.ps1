# SPDX-License-Identifier: MIT
$ErrorActionPreference='Stop'
$taskRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=Join-Path $taskRepo 'source/.build/ecu-tests'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskRuntime=Join-Path $taskRepo 'GameData/KDAVehicles/Plugins/KDAVehicles.dll'
$taskFiles=@('EcuCore.cs','AddonProfiles.cs','EcuWheel.cs','Tests/WheelDoubles.cs','Tests/AdapterTests.cs','Tests/AddonTests.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /optimize+ ('/out:'+(Join-Path $taskOutput 'EcuTests.exe')) ('/reference:'+$taskRuntime) $taskFiles
if($LASTEXITCODE -ne 0){throw 'ECU test compilation failed'}
Copy-Item -LiteralPath $taskRuntime -Destination (Join-Path $taskOutput 'KDAVehicles.dll')
& (Join-Path $taskOutput 'EcuTests.exe')
if($LASTEXITCODE -ne 0){throw 'ECU tests failed'}
