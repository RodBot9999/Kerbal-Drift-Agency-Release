param([string]$Compiler='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
$ErrorActionPreference='Stop'
$taskSource=Split-Path -Parent $PSScriptRoot
$taskOutput=Join-Path $taskSource '.build/tests/hybrid-audio-tests.exe'
New-Item -ItemType Directory -Path (Split-Path -Parent $taskOutput) -Force | Out-Null
$taskFiles=@('plugin/HybridAudioCore.cs','plugin/KDAHybridAudio.cs','plugin/Tests/HybridAudioTests.cs','plugin/Tests/UnityAudioStubs.cs') | ForEach-Object {Join-Path $taskSource $_}
& $Compiler /nologo /optimize+ /out:$taskOutput $taskFiles
if($LASTEXITCODE -ne 0){throw 'Audio test compilation failed'}
& $taskOutput
if($LASTEXITCODE -ne 0){throw 'Audio behavior tests failed'}
