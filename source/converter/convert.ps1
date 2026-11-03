param(
 [Parameter(Mandatory=$true)][string]$ExportZip,
 [string]$Output='converter-output\new-car',
 [ValidateSet('quality','balanced','lightweight')][string]$Quality='balanced',
 [string]$Python='python'
)
$ErrorActionPreference='Stop'
$taskProject=Split-Path $PSScriptRoot -Parent
$taskOldPath=$env:PYTHONPATH
try {
 $env:PYTHONPATH=$PSScriptRoot
 & $Python -m kdaconvert $ExportZip --output $Output --quality $Quality
 if($LASTEXITCODE -ne 0){throw 'Conversion failed. See the diagnostic above.'}
} finally { $env:PYTHONPATH=$taskOldPath }
