# SPDX-License-Identifier: MIT
param([string]$TelemetryCsv='')
$ErrorActionPreference='Stop'
$taskSource=Split-Path -Parent $PSScriptRoot
$taskOutput=Join-Path $taskSource '.build/tests/extreme-governor-tests.exe'
New-Item -ItemType Directory -Path (Split-Path -Parent $taskOutput) -Force | Out-Null
$taskFiles=@('KDACore.cs','DriverAidsCore.cs','DrivelineCore.cs','ActiveDrivelineCore.cs','ExtremeGovernorTests.cs') | ForEach-Object {Join-Path $taskSource ('plugin/'+$_)}
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /optimize+ /reference:System.Core.dll ('/out:'+$taskOutput) $taskFiles
if($LASTEXITCODE -ne 0){throw 'Governor tests failed to compile'}
if($TelemetryCsv){& $taskOutput ([IO.Path]::GetFullPath($TelemetryCsv))}else{& $taskOutput}
if($LASTEXITCODE -ne 0){throw 'Governor tests failed'}
