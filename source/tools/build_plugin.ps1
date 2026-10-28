param([string]$KspRoot='C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program',[string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskManaged=Join-Path $KspRoot 'KSP_x64_Data\Managed'
$taskCompiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskStage=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $taskRoot '.build/runtime/GameData'}
$taskOutput=Join-Path $taskStage 'KDAVehicles/Plugins'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskReferences=@('Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.UI.dll','UnityEngine.AnimationModule.dll','UnityEngine.UIModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.VehiclesModule.dll','UnityEngine.AudioModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.ParticleSystemModule.dll','UnityEngine.TextRenderingModule.dll','UnityEngine.ImageConversionModule.dll') | ForEach-Object { '/reference:'+(Join-Path $taskManaged $_) }
$taskReferences+=('/reference:'+(Join-Path $KspRoot 'GameData/000_Harmony/0Harmony.dll'))
$taskSources=@('HybridAudioCore.cs','KDAHybridAudio.cs','AfterfireStyleCore.cs','GarageEntranceCore.cs','NitrousCore.cs','KDANitrous.cs','KDAGarageNitrous.cs','LongTravelCore.cs','KDALongTravel.cs','KDALongTravelVisual.cs','KDAGarageTravel.cs','KDAGarageInput.cs','KDAGarageExtraTuning.cs','KDAAfterfire.cs','KDAGarageDelete.cs','KDAGarageBrand.cs','KDAGarageLoading.cs','KDAGarageExhaust.cs','GaragePowerRating.cs','KDAGarageLook.cs','GarageRollCore.cs','KDAGarageAnalysis.cs','KDAGarageLauncher.cs','KDAGaragePrebuild.cs','KDAGarageCollection.cs','KDAGarageEditor.cs','KDAWorldGarage.cs','KDAWorkshopRoom.cs','KDAGarage.cs','KDADriverCamera.cs','KDAInterior.cs','KDAExteriorDepth.cs','LightingCore.cs','KDALighting.cs','KDAWarpGrounding.cs','KDATelemetry.cs','TelemetryCsv.cs','TelemetryGeometryCore.cs','KDASuspensionVisual.cs','DrivelineCore.cs','AwdDriftCore.cs','AwdCorneringCore.cs','ActiveDrivelineCore.cs','KDAWheelSelection.cs','KDAAero.cs','EngineCore.cs','InductionCore.cs','GearboxCore.cs','TuningCore.cs','DrivingEffectsCore.cs','ExhaustCore.cs','SuspensionCore.cs','DriverAidsCore.cs','KDACore.cs','RoadSportCore.cs','KDAController.cs','KDAImportedMass.cs','KDASuspension.cs','KDASteeringWheel.cs','KDAModules.cs','KDAAudio.cs','KDASampleAudio.cs','KDAWheelBrakes.cs','KDABurnout.cs','KDATireSmoke.cs','KDAHud.cs','KDAExhaust.cs','KDACarControls.cs','KDAAxle.cs','KDAProtection.cs') | ForEach-Object {Join-Path "$taskRoot\plugin" $_}
& $taskCompiler /nologo /target:library /optimize+ /out:"$taskOutput\KDAVehicles.dll" $taskReferences $taskSources ('/resource:'+(Join-Path $taskRoot 'plugin/Assets/Sparky.png')+',KDA.Sparky.png')
if($LASTEXITCODE -ne 0){throw 'Plugin compilation failed'}
$taskRelease=Get-Content (Join-Path $taskRoot '../GameData/KDAVehicles/framework.json') -Raw | ConvertFrom-Json
$taskStamp=@{abi=$taskRelease.abi;build=$taskRelease.build;runtime_sha256=(Get-FileHash (Join-Path $taskOutput 'KDAVehicles.dll') -Algorithm SHA256).Hash.ToLowerInvariant()} | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path (Split-Path $taskOutput -Parent) 'framework.json'),$taskStamp)
Write-Output ('Built runtime in '+$taskOutput)
