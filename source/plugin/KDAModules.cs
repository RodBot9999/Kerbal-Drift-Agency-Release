using System;
using System.Collections.Generic;
using System.Globalization;
using ModuleWheels;
using UnityEngine;
using KerbalDriftAgency;

public partial class KDAEngine : PartModule {
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Throttle buildup (s)",guiFormat="F2")]
 [UI_FloatRange(minValue=0,maxValue=.8f,stepIncrement=.05f)] public float throttleRiseSeconds=.25f;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Throttle release (s)",guiFormat="F2")]
 [UI_FloatRange(minValue=0,maxValue=.2f,stepIncrement=.02f)] public float throttleReleaseSeconds=.06f;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Differential")]
 [UI_ChooseOption(options=new[]{"Open","Mild LSD","Sport LSD","Drift LSD"})] public int differentialMode=1;
 double pedal;
 readonly Dictionary<KDAWheelMotor,double> differentialWeights=new Dictionary<KDAWheelMotor,double>();
 // Filter state precedes the temporary AWD handbrake redistribution. Feeding
 // that redistribution back into the next LSD step can exceed an axle's
 // allocation and create a negative opposite-wheel torque.
 readonly Dictionary<KDAWheelMotor,double> passiveDifferentialWeights=new Dictionary<KDAWheelMotor,double>();
 [KSPField] public bool importedActiveCenter=false,importedFrontVectoring=false,importedRearVectoring=false;
 [KSPField] public float activeCenterOffset=.2f,activeCenterRate=1.5f,activeCenterSpeedThreshold=10,activeCenterDifferenceThreshold=1;
 [KSPField] public float frontVectoringOffset=.2f,rearVectoringOffset=.2f,frontVectoringRate=1.5f,rearVectoringRate=1.5f;
 [KSPField(guiActiveEditor=true,guiActive=true,guiName="Imported driveline")] public string drivelineHardware="";
 [KSPField(guiActive=true,guiName="Rear drive allocation",guiFormat="P0")] public float rearDriveFraction=.5f;
 [KSPField] public float frontLeftDriveFraction=.5f,rearLeftDriveFraction=.5f,vectorYawError;
 readonly ActiveDrivelineCore activeDriveline=new ActiveDrivelineCore();
 readonly AwdHandbrakeClutch awdHandbrakeClutch=new AwdHandbrakeClutch();
 public float ActiveYawTarget {get;private set;}
 public float DriftRearMinimum {get;private set;}
 public float PoweredRearMinimum {get;private set;}
 public float RearDrivelineCoupling {get{return (float)awdHandbrakeClutch.Coupling;}}
 public bool ImportedAwdDriveline {get{
  if(!importedVehicle||vessel==null)return false;bool front=false,rear=false;
  foreach(Part p in vessel.parts){if(p.parent!=part)continue;var m=p.FindModuleImplementing<KDAWheelMotor>();if(m==null||!m.motorEnabled||m.DriveWeight<=0)continue;if(part.transform.InverseTransformPoint(p.transform.position).y>0)front=true;else rear=true;}
  return front&&rear;
 }}
 public float DriveClutchForWheel(KDAWheelMotor motor){return awdHandbrakeClutch.Coupling<1&&motor!=null&&motor.part.parent==part&&part.transform.InverseTransformPoint(motor.part.transform.position).y<=0?(float)awdHandbrakeClutch.Coupling:1;}
 public bool ActiveCenterHardware {get{return importedVehicle&&importedActiveCenter;}}
 public bool FrontVectoringHardware {get{return importedVehicle&&importedFrontVectoring;}}
 public bool RearVectoringHardware {get{return importedVehicle&&importedRearVectoring;}}
 [KSPField(isPersistant=true)] public bool engineRunning=false;
 [KSPField(isPersistant=true)] public bool importedVehicle=false,importedBoostCalibration=false;
 [KSPField] public float importedPeakBoostBar=0;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Imported engine")] public string importedEngineName="";
 [KSPField] public float idleRpm=900,launchRpm=2400;
 [KSPField] public float referenceRpm=6500,maximumSupportedRpm=9000,drivenWheelRadius=.296f;
 [KSPField(isPersistant=true,guiName="Keep imported ratios")] public bool preserveImportedRatios=false;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="RPM limit",guiFormat="F0")]
 [UI_FloatRange(minValue=5500,maxValue=9000,stepIncrement=250)] public float revLimitRpm=6500;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Race rev limiter")] public bool raceLimiter=true;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Forward gears",guiFormat="F0")]
 [UI_FloatRange(minValue=4,maxValue=6,stepIncrement=1)] public float forwardGears=5;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Gear spacing",guiFormat="F2")]
 [UI_FloatRange(minValue=2,maxValue=6,stepIncrement=.25f)] public float gearSpacing=3.7f;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Geared top speed",guiUnits=" km/h",guiFormat="F0")]
 [UI_FloatRange(minValue=140,maxValue=420,stepIncrement=10)] public float gearedTopSpeed=200;
 [KSPField(isPersistant=true)] public int transmissionSetup=0,engineInternals=0;
 [KSPField(guiActiveEditor=true,guiName="Gearbox setup")] public string gearboxSetupName="Automatic";
 [KSPField(guiActiveEditor=true,guiName="Engine internals")] public string internalsName="Stock";
 [KSPField(guiActiveEditor=true,guiName="Ratios")] public string ratioSummary="";
 [KSPField] public float fixedGearRatio=.95f, finalDrive=3.89f, drivetrainEfficiency=.90f;
 [KSPField] public float maxBoostBar=.8f, boostStartRpm=1800, boostFullRpm=3600;
 [KSPField] public float spoolSeconds=1.1f, boostReleaseSeconds=.28f, boostTorqueGain=.75f;
 [KSPField] public float mechanicalBoostMaxBar=0,superchargerEngageRpm=0,superchargerDisengageRpm=0;
 [KSPField] public float shiftBoostRetention=.40f,shiftBoostReleaseSeconds=.12f;
 [KSPField] public float rpmResponseSeconds=.22f, idleFuelRate=.0015f, fullFuelRate=.024f;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiActive=false,guiName="Fuel consumption",guiUnits=" x",guiFormat="F1")]
 [UI_FloatRange(minValue=.1f,maxValue=3,stepIncrement=.1f)] public float fuelConsumptionMultiplier=1;
 [KSPField] public float alternatorRate=.2f;
 [KSPField] public float engineBrakingScale=1;
 [KSPField(isPersistant=true)] public int selectedGear=1, driveSelector=1;
 [KSPField(isPersistant=true)] public bool automaticShifting=true;
 [KSPField(isPersistant=true)] public int inductionSetup=0;
 [KSPField(guiActiveEditor=true,guiName="Induction")] public string inductionName="Stock turbo";
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Boost kick",guiFormat="F2")]
 [UI_FloatRange(minValue=0,maxValue=1,stepIncrement=.05f)] public float boostKick=0;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Boost controller",guiUnits="%",guiFormat="F0")]
 [UI_FloatRange(minValue=0,maxValue=100,stepIncrement=5)] public float boostController=100;
 [KSPField] public float reverseRatio=3.4f,shiftSeconds=.30f,minimumShiftInterval=.85f;
 [KSPField(guiActive=true,guiName="Gear")] public string gearDisplay="D1";
 [KSPField(guiActive=true,guiName="Transmission")] public string transmissionDisplay="Automatic";
 [KSPField(guiActive=true,guiName="Engine")] public string status="Off";
 [KSPField(guiActive=true,guiName="Engine RPM",guiFormat="F0")] public float engineRpm;
 [KSPField(guiActive=true,guiName="Turbo boost",guiUnits=" bar",guiFormat="F2")] public float boostBar;
 [KSPField(guiActive=true,guiName="Speed",guiUnits=" km/h",guiFormat="F1")] public float speedKmh;
 [KSPField(guiActive=true,guiName="Engine torque",guiUnits=" Nm",guiFormat="F0")] public float torqueNm;
 readonly EngineCore core=new EngineCore();
 readonly GearboxCore gearbox=new GearboxCore();
 KDABurnout burnout;
 double currentWheelRpm;
 bool curveLoaded,ratiosLoaded;
 float lastStep=-1;
 int drivenCount;
 double driveWeights;
 readonly List<KDAWheelMotor> motors=new List<KDAWheelMotor>();

 public override void OnLoad(ConfigNode node) {
  base.OnLoad(node);
  LoadEngineData(node);
 }
 void LoadEngineData(ConfigNode node) {
  string ratios=node.GetValue("forwardRatios");
  if(ratios!=null){var values=new List<double>();foreach(string text in ratios.Split(',')){
   double value;if(double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&value>.1&&value<8)values.Add(value);
  }if(values.Count>=2){gearbox.Ratios=values.ToArray();ratiosLoaded=true;}}
  ConfigNode pressure=node.GetNode("importedBoostCurve");
  if(pressure!=null){var ps=new SortedDictionary<double,double>();foreach(string key in pressure.GetValues("key")){string[] f=key.Split(new char[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);double r,b;if(f.Length>=2&&double.TryParse(f[0],NumberStyles.Float,CultureInfo.InvariantCulture,out r)&&double.TryParse(f[1],NumberStyles.Float,CultureInfo.InvariantCulture,out b)&&!double.IsNaN(r)&&!double.IsInfinity(r)&&!double.IsNaN(b)&&!double.IsInfinity(b)&&r>=0&&b>=0)ps[r]=b;}
   core.BoostRpm=new double[ps.Count];core.BoostGauge=new double[ps.Count];int pi=0;foreach(var p in ps){core.BoostRpm[pi]=p.Key;core.BoostGauge[pi++]=p.Value;}
  }
  ConfigNode curve=node.GetNode("engineTorqueCurve");
  if(curve==null)return;
  var points=new SortedDictionary<double,double>();
  foreach(string key in curve.GetValues("key")) {
   string[] fields=key.Split(new char[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
   double rpm,torque;
   if(fields.Length>=2 && double.TryParse(fields[0],NumberStyles.Float,CultureInfo.InvariantCulture,out rpm)
    && double.TryParse(fields[1],NumberStyles.Float,CultureInfo.InvariantCulture,out torque)
    && !double.IsNaN(rpm) && !double.IsInfinity(rpm) && !double.IsNaN(torque) && !double.IsInfinity(torque)
    && rpm>=0 && torque>=0)points[rpm]=torque;
  }
  if(points.Count>=2) {curveLoaded=true;core.CurveRpm=new double[points.Count];core.CurveTorque=new double[points.Count];int i=0;
   foreach(var point in points){core.CurveRpm[i]=point.Key;core.CurveTorque[i++]=point.Value;}}
 }
 public override void OnStart(StartState state) {
  base.OnStart(state);
  burnout=part.FindModuleImplementing<KDABurnout>();
  if((importedVehicle || !curveLoaded) && part.partInfo!=null && part.partInfo.partConfig!=null)
   foreach(ConfigNode module in part.partInfo.partConfig.GetNodes("MODULE"))
    if(module.GetValue("name")=="KDAEngine"){
     LoadActiveDriveline(module);
     // The fallback supplies non-persistent calibration and the torque curve.
     // Calling OnLoad here also reloads saved tuning and PAW event flags.
     // Leave persistent engine/gearbox selections and events intact.
     foreach(var field in typeof(KDAEngine).GetFields()){
      var attributes=field.GetCustomAttributes(typeof(KSPField),true);
      if(attributes.Length==0||((KSPField)attributes[0]).isPersistant||field.FieldType!=typeof(float))continue;
      float calibration;if(float.TryParse(module.GetValue(field.Name),NumberStyles.Float,CultureInfo.InvariantCulture,out calibration))field.SetValue(this,calibration);
     }
     // Imported engines are immutable source calibrations. Unity/KSP part
     // cloning and old craft saves must never substitute the default core curve.
     ConfigNode data=module.CreateCopy();if(ratiosLoaded && !(importedVehicle && preserveImportedRatios))data.RemoveValues("forwardRatios");LoadEngineData(data);
    }
  var rpmUi=Fields["revLimitRpm"].uiControlEditor as UI_FloatRange;if(rpmUi!=null)rpmUi.maxValue=maximumSupportedRpm;
  Fields["preserveImportedRatios"].guiActiveEditor=referenceRpm!=6500;
  if(importedVehicle){transmissionSetup=(int)PrefabValue("transmissionSetup",transmissionSetup);inductionSetup=(int)PrefabValue("inductionSetup",inductionSetup);engineInternals=0;
   foreach(string name in new[]{"NextTransmission","NextInduction","NextInternals"})Events[name].guiActiveEditor=false;
   Fields["boostKick"].guiActiveEditor=false;Fields["internalsName"].guiActiveEditor=false;}
  ApplyInduction();Configure(); RefreshEvents();
  Fields["drivelineHardware"].guiActive=Fields["drivelineHardware"].guiActiveEditor=ActiveCenterHardware||FrontVectoringHardware||RearVectoringHardware;
  Fields["rearDriveFraction"].guiActive=ActiveCenterHardware;
  if(FrontVectoringHardware||RearVectoringHardware)Fields["differentialMode"].guiName="Passive axle differential";
  Debug.Log("[KDA] RPM/turbo engine ready on "+part.name);
 }
 public override void OnSave(ConfigNode node) {
  if(HighLogic.LoadedSceneIsEditor)SetupGearing();
  base.OnSave(node);node.RemoveNodes("engineTorqueCurve");
  if(BurnoutActive)node.SetValue("selectedGear",burnout.PreviousGear.ToString(CultureInfo.InvariantCulture),true);
  node.SetValue("forwardRatios",String.Join(",",Array.ConvertAll(gearbox.Ratios,r=>r.ToString(CultureInfo.InvariantCulture))),true);
  node.RemoveNodes("importedBoostCurve");ConfigNode pressure=node.AddNode("importedBoostCurve");
  for(int j=0;j<core.BoostRpm.Length;j++)pressure.AddValue("key",core.BoostRpm[j].ToString(CultureInfo.InvariantCulture)+" "+core.BoostGauge[j].ToString(CultureInfo.InvariantCulture));
  ConfigNode curve=node.AddNode("engineTorqueCurve");
  for(int i=0;i<core.CurveRpm.Length;i++)curve.AddValue("key",core.CurveRpm[i].ToString(CultureInfo.InvariantCulture)+" "+core.CurveTorque[i].ToString(CultureInfo.InvariantCulture));
 }
 void Configure() {
  SetupGearing();
  core.IdleRpm=EngineCore.ConfiguredIdle(idleRpm,importedVehicle);core.LimitRpm=Mathf.Clamp(revLimitRpm,5500,maximumSupportedRpm);
  core.LaunchRpm=Mathf.Clamp(launchRpm,coreIdle(),(float)core.LimitRpm-500);
  core.GearRatio=Mathf.Clamp(fixedGearRatio,.2f,5);core.FinalDrive=Mathf.Clamp(finalDrive,1,8);
  core.Efficiency=Mathf.Clamp(drivetrainEfficiency,.1f,1);core.MaxBoost=Mathf.Clamp(maxBoostBar,0,importedBoostCalibration?8:2);
  core.BoostStartRpm=Mathf.Max(500,boostStartRpm);core.BoostFullRpm=Mathf.Max(boostStartRpm+100,boostFullRpm);
  core.SpoolSeconds=Mathf.Clamp(spoolSeconds,.05f,10);core.ReleaseSeconds=Mathf.Clamp(boostReleaseSeconds,.05f,5);
  core.ShiftBoostRetention=Mathf.Clamp01(shiftBoostRetention);core.ShiftReleaseSeconds=Mathf.Clamp(shiftBoostReleaseSeconds,.05f,1);
  core.BoostTorqueGain=Mathf.Clamp(boostTorqueGain,0,2);core.RpmResponseSeconds=Mathf.Clamp(rpmResponseSeconds,.05f,2);
  core.IdleFuelRate=Mathf.Max(0,idleFuelRate);core.FullFuelRate=Mathf.Max(idleFuelRate,fullFuelRate)*(1+.10*Math.Max(0,Math.Min(2,engineInternals)));
  core.FuelConsumptionMultiplier=FuelConsumptionCore.Multiplier(fuelConsumptionMultiplier);
  core.TorqueRpmScale=referenceRpm/core.LimitRpm;
  core.RaceLimiter=raceLimiter;InductionCore.Configure(core,inductionSetup);
  core.ImportedBoostMap=importedBoostCalibration&&(inductionSetup==0||inductionSetup==4||inductionSetup==6);core.ImportedPeakBoost=importedPeakBoostBar;
  core.TwinCharged=importedBoostCalibration&&inductionSetup==6;
  core.MechanicalMaxBoost=Mathf.Clamp(mechanicalBoostMaxBar,0,8);
  core.MechanicalEngageRpm=Mathf.Max(0,superchargerEngageRpm);core.MechanicalDisengageRpm=Mathf.Max(0,superchargerDisengageRpm);
  gearbox.FinalDrive=core.FinalDrive;gearbox.ReverseRatio=Mathf.Clamp(reverseRatio,.2f,6);
  gearbox.ShiftSeconds=transmissionSetup==2?.06:Mathf.Clamp(shiftSeconds,.1f,1);gearbox.MinimumShiftInterval=transmissionSetup==2?.12:Mathf.Clamp(minimumShiftInterval,.3f,3);
  gearbox.Selector=Math.Sign(driveSelector);gearbox.Gear=Math.Max(1,Math.Min(gearbox.Ratios.Length,selectedGear));gearbox.Automatic=automaticShifting;
 }
 float coreIdle(){return (float)core.IdleRpm;}
 void Update(){if(HighLogic.LoadedSceneIsEditor)SetupGearing();}
 void SetupGearing(){
  transmissionSetup=Math.Max(0,Math.Min(2,transmissionSetup));engineInternals=Math.Max(0,Math.Min(2,engineInternals));
  gearboxSetupName=new[]{"Automatic","Manual","Sequential"}[transmissionSetup];internalsName=new[]{"Stock","Sport","Race"}[engineInternals];
  automaticShifting=transmissionSetup==0;
  foreach(string field in new[]{"forwardGears","gearSpacing","gearedTopSpeed"})Fields[field].guiActiveEditor=!preserveImportedRatios;
  if(!preserveImportedRatios)gearbox.Ratios=TuningCore.Ratios((int)Math.Round(forwardGears),gearSpacing,gearedTopSpeed,Mathf.Clamp(revLimitRpm,5500,maximumSupportedRpm),drivenWheelRadius,Mathf.Clamp(finalDrive,1,8));
  ratioSummary=String.Join(" / ",Array.ConvertAll(gearbox.Ratios,r=>r.ToString("F2",CultureInfo.InvariantCulture)));
 }
 [KSPEvent(guiActiveEditor=true,guiName="Next gearbox type",active=true)]
 public void NextTransmission(){if(importedVehicle||!HighLogic.LoadedSceneIsEditor)return;transmissionSetup=(transmissionSetup+1)%3;Configure();}
 [KSPEvent(guiActiveEditor=true,guiName="Next engine internals",active=true)]
 public void NextInternals(){if(importedVehicle||!HighLogic.LoadedSceneIsEditor)return;engineInternals=(engineInternals+1)%3;revLimitRpm=Mathf.Min(maximumSupportedRpm,referenceRpm+1000*engineInternals);Configure();}
 [KSPEvent(guiActiveEditor=true,guiName="Next induction setup",active=true)]
 public void NextInduction(){if(importedVehicle||!HighLogic.LoadedSceneIsEditor)return;inductionSetup=(inductionSetup+1)%6;ApplyInduction();Configure();}
 void ApplyInduction(){
  inductionSetup=Math.Max(0,Math.Min(importedVehicle?6:5,inductionSetup));
  if(importedVehicle){
   inductionName=inductionSetup==6?"Stock twincharged":inductionSetup==4?"Stock supercharger":inductionSetup==5?"Naturally aspirated":"Stock turbo";
   return; // Keep the imported pressure calibration, not generic tuning presets.
  }
  inductionName=new[]{"Stock turbo","Small single turbo","Twin turbo","Big single turbo","Supercharger","Naturally aspirated"}[inductionSetup];
  if(inductionSetup==0){
   // Return to ModuleManager-patched calibration, rather than the previous preset.
   maxBoostBar=PrefabValue("maxBoostBar",.8f);boostStartRpm=PrefabValue("boostStartRpm",1800);
   boostFullRpm=PrefabValue("boostFullRpm",3600);spoolSeconds=PrefabValue("spoolSeconds",1.1f);
  }else{
   float[][] setups={new[]{.55f,1300,2600,.50f},new[]{1.02f,1800,3400,.65f},new[]{1.56f,3200,3550,.75f},new[]{.55f,900,1200,.10f},new[]{0f,1800,3600,1.1f}};
   var setup=setups[inductionSetup-1];maxBoostBar=setup[0];boostStartRpm=setup[1];boostFullRpm=setup[2];spoolSeconds=setup[3];
  }
 }
 float PrefabValue(string key,float fallback){
  if(part.partInfo!=null&&part.partInfo.partConfig!=null)foreach(ConfigNode module in part.partInfo.partConfig.GetNodes("MODULE"))if(module.GetValue("name")=="KDAEngine"){
   float value;if(float.TryParse(module.GetValue(key),NumberStyles.Float,CultureInfo.InvariantCulture,out value))return value;
  }return fallback;
 }
 public float RequestedThrottle {get;private set;}
 public bool HasTurbo {get{return inductionSetup<4||inductionSetup==6;}}
 [KSPEvent(guiActive=true,guiActiveEditor=true,guiName="Engine ON",active=true)]
 public void StartEngine(){engineRunning=true;RefreshEvents();}
 [KSPEvent(guiActive=true,guiActiveEditor=true,guiName="Engine OFF",active=true)]
 public void StopEngine(){engineRunning=false;RefreshEvents();}
 [KSPAction("Toggle engine")] public void ToggleEngine(KSPActionParam p){engineRunning=!engineRunning;RefreshEvents();}
 [KSPEvent(guiActive=false,guiActiveEditor=false,guiName="Toggle automatic/manual",active=false)]
 public void ToggleAutomatic(){if(importedVehicle||!HighLogic.LoadedSceneIsEditor)return;transmissionSetup=transmissionSetup==0?1:0;Configure();}
 [KSPEvent(guiActive=true,guiName="Shift UP (manual)",active=true)]
 public void ShiftUp(){ManualShift(1);}
 [KSPEvent(guiActive=true,guiName="Shift DOWN (manual)",active=true)]
 public void ShiftDown(){ManualShift(-1);}
 void ManualShift(int direction){
  if(BurnoutActive)return;
  if(!gearbox.RequestManualShift(direction,currentWheelRpm,core.LimitRpm))ScreenMessages.PostScreenMessage("Shift unavailable: gear limit, shift delay or RPM protection",2,ScreenMessageStyle.UPPER_CENTER);
  selectedGear=gearbox.Gear;
 }
 [KSPEvent(guiActive=true,guiName="Resume automatic shifting",active=true)] public void ResumeAutomatic(){gearbox.ResumeAutomatic();}
 [KSPAction("Resume automatic shifting")] public void ResumeAutomaticAction(KSPActionParam p){ResumeAutomatic();}
 [KSPEvent(guiActive=true,guiName="Select DRIVE",active=true)] public void SelectDrive(){SelectRange(1);}
 [KSPEvent(guiActive=true,guiName="Select NEUTRAL",active=true)] public void SelectNeutral(){SelectRange(0);}
 [KSPEvent(guiActive=true,guiName="Select REVERSE",active=true)] public void SelectReverse(){SelectRange(-1);}
 void SelectRange(int value){if(!gearbox.Select(value,vessel==null?0:vessel.srfSpeed))ScreenMessages.PostScreenMessage("Slow below 3.6 km/h before selecting Drive or Reverse",3,ScreenMessageStyle.UPPER_CENTER);driveSelector=gearbox.Selector;selectedGear=gearbox.Gear;}
 [KSPAction("Shift up")] public void ShiftUpAction(KSPActionParam p){ShiftUp();}
 [KSPAction("Shift down")] public void ShiftDownAction(KSPActionParam p){ShiftDown();}
 [KSPAction("Toggle automatic/manual")] public void ToggleAutoAction(KSPActionParam p){ToggleAutomatic();}
 [KSPAction("Select Drive")] public void DriveAction(KSPActionParam p){SelectDrive();}
 [KSPAction("Select Neutral")] public void NeutralAction(KSPActionParam p){SelectNeutral();}
 [KSPAction("Select Reverse")] public void ReverseAction(KSPActionParam p){SelectReverse();}
 void RefreshEvents(){
  foreach(string name in new[]{"StartEngine","StopEngine","ShiftUp","ShiftDown","ResumeAutomatic","SelectDrive","SelectNeutral","SelectReverse"}){
   var evt=Events[name];evt.guiActive=true;evt.guiActiveEditor=name=="StartEngine"||name=="StopEngine";evt.active=true;
  }
  Events["StartEngine"].active=!engineRunning;Events["StopEngine"].active=engineRunning;
 }
 public override void OnFixedUpdate(){EnsureStep();}
 public void EnsureStep() {
  if(!HighLogic.LoadedSceneIsFlight || vessel==null || vessel.packed || lastStep==Time.fixedTime)return;
  lastStep=Time.fixedTime;
  if(burnout!=null)burnout.Evaluate();
  var carControls=part.FindModuleImplementing<KDACarControls>();if(carControls!=null)carControls.Apply();
  motors.Clear();drivenCount=0;driveWeights=0;double wheelRpm=0,axleLoad=0;
  foreach(Part p in vessel.parts)foreach(KDAWheelMotor m in p.FindModulesImplementing<KDAWheelMotor>()) {
   motors.Add(m);
   if(m.motorEnabled && m.CanDrive){drivenCount++;driveWeights+=m.DriveWeight;wheelRpm+=m.WheelRpm*m.DriveRatio*m.DriveWeight;axleLoad+=m.EstimatedLoad;}
  }
  UpdateAwdClutch(Math.Min(.1,TimeWarp.fixedDeltaTime),carControls,ref wheelRpm,ref axleLoad);
  if(driveWeights>0)wheelRpm/=driveWeights;
  UpdateDifferential(Math.Min(.1,TimeWarp.fixedDeltaTime));
  ApplyAwdClutchAllocation();
  currentWheelRpm=wheelRpm;
  double throttle=Mathf.Clamp(vessel.ctrlState.wheelThrottle+vessel.ctrlState.wheelThrottleTrim,-1,1);
  if(drivenCount==0)throttle=0;
  double dt=Math.Min(.1,TimeWarp.fixedDeltaTime);
  // W drives in D; S drives in R. Opposite input cannot reverse a moving car.
  double drivingThrottle=gearbox.Selector==0?throttle:gearbox.Selector>0?Math.Max(0,throttle):Math.Min(0,throttle);
  if(BurnoutActive)drivingThrottle=1;
  var kda=part.FindModuleImplementing<KDAController>();
  bool roadProfile=kda!=null&&RoadSportCore.Applies(kda.driveMode)&&kda.stabilityEnabled&&kda.assistance>0&&!BurnoutActive&&!BurnoutLaunching;
  double pedalRise=Mathf.Clamp(throttleRiseSeconds,0,.8f);
  if(roadProfile)pedalRise=RoadSportCore.ThrottleRise(kda.driveMode,true,pedalRise);
  pedal=DrivelineCore.Pedal(pedal,engineRunning?drivingThrottle:0,dt,pedalRise,Mathf.Clamp(throttleReleaseSeconds,0,.2f));
  drivingThrottle=BurnoutActive?1:pedal;
  if(kda!=null&&kda.stabilityEnabled&&carControls!=null&&carControls.TcsActive&&!BurnoutActive&&!BurnoutLaunching)drivingThrottle*=kda.ThrottleScale;
  gearbox.Automatic=automaticShifting&&!BurnoutActive&&!BurnoutLaunching;
  if(BurnoutActive)gearbox.Gear=1;
  gearbox.Step(dt,engineRunning,drivingThrottle,wheelRpm,core.Rpm,core.LimitRpm);
  core.GearRatio=gearbox.Ratio;core.TransmissionScale=gearbox.TorqueFactor;
  core.Shifting=gearbox.Shifting;
  RequestedThrottle=(float)Math.Abs(drivingThrottle);
  core.SpoolExponent=HasTurbo?1+2*Mathf.Clamp01(boostKick):1;
  core.ParasiticFactor=inductionSetup==4&&!importedBoostCalibration?.90:1;
  core.MaxBoost=Mathf.Clamp(maxBoostBar,0,importedBoostCalibration?8:2)*Mathf.Clamp01(boostController*.01f);
  core.EngineLoad=drivenCount>0?axleLoad/drivenCount:0;
  core.LaunchRpm=BurnoutActive?Math.Min(core.LimitRpm-500,Math.Max(launchRpm,boostFullRpm+300)):BurnoutLaunching?Math.Min(core.LimitRpm,burnout.LaunchFloor):Mathf.Clamp(launchRpm,coreIdle(),(float)core.LimitRpm-500);
  core.Step(dt,engineRunning,drivingThrottle,wheelRpm);
  selectedGear=gearbox.Gear;driveSelector=gearbox.Selector;
  gearDisplay=gearbox.Selector>0?"D"+gearbox.Display:gearbox.Display;
  transmissionDisplay=gearboxSetupName+(gearbox.ManualOverride?" (manual override)":"")+(gearbox.Shifting?" (shifting)":"");
  if(engineRunning) {
   double needed=core.FuelRate*dt;
   double received=needed>0?part.RequestResource("LiquidFuel",needed):needed;
   if(needed>0 && received<needed*.99) {
    engineRunning=false;core.AvailableTorqueNm=0;core.OutputTorqueNm=0;status="Out of fuel";RefreshEvents();
   }else {
    if(alternatorRate>0)part.RequestResource("ElectricCharge",-alternatorRate*dt);
    status=core.LimiterActive?"Rev limiter":drivenCount==0?"No driven wheels":"Running";
   }
  }else if(status!="Out of fuel")status="Off";
  StepNitrous(dt);
  engineRpm=(float)core.Rpm;boostBar=(float)core.Boost;torqueNm=(float)core.OutputTorqueNm;
  speedKmh=(float)(vessel.srfSpeed*3.6);
 }
 public float TorquePerWheel(){EnsureStep();return (float)core.WheelTorqueKnM(drivenCount);}
 public float RoadSportPedal {get{return (float)Math.Abs(pedal);}}
 public float TorqueForWheel(KDAWheelMotor m){EnsureStep();double weight; if(!differentialWeights.TryGetValue(m,out weight))weight=m.DriveWeight;return driveWeights>0?(float)(core.WheelTorqueKnM(1)*weight*m.DriveRatio/driveWeights):0;}
 void UpdateDifferential(double dt){
  if((ActiveCenterHardware||FrontVectoringHardware||RearVectoringHardware)&&TryActiveDifferential(dt))return;
  activeDriveline.Reset();vectorYawError=ActiveYawTarget=DriftRearMinimum=PoweredRearMinimum=0;
  var active=new HashSet<KDAWheelMotor>();var paired=new HashSet<KDAWheelMotor>();
  foreach(var m in motors)if(m.motorEnabled&&m.CanDrive){active.Add(m);if(!differentialWeights.ContainsKey(m))differentialWeights[m]=m.DriveWeight;}
  foreach(var m in motors){
   if(!active.Contains(m)||paired.Contains(m))continue;
   Vector3 pos=part.transform.InverseTransformPoint(m.part.transform.position);
   KDAWheelMotor other=null;float nearest=.5f;
   foreach(var n in motors){if(n==m||!active.Contains(n)||paired.Contains(n))continue;
    Vector3 p=part.transform.InverseTransformPoint(n.part.transform.position);
    if(pos.x*p.x>=0)continue;float distance=Mathf.Abs(pos.y-p.y);if(distance<nearest){nearest=distance;other=n;}
   }
   paired.Add(m);
   if(other==null){differentialWeights[m]=m.DriveWeight;continue;}
   paired.Add(other);double total=m.DriveWeight+other.DriveWeight;
   double share=DrivelineCore.LeftShare(m.DriveWeight,other.DriveWeight,m.LongitudinalSlip,other.LongitudinalSlip,differentialMode);
   double target=total*share;
   double previous;if(!passiveDifferentialWeights.TryGetValue(m,out previous))previous=m.DriveWeight;
   previous=EngineCore.Clamp(previous,0,total);double change=total*dt/(differentialMode==3?.08:.2);
   double value=EngineCore.Clamp(previous+Math.Max(-change,Math.Min(change,target-previous)),0,total);
   passiveDifferentialWeights[m]=value;passiveDifferentialWeights[other]=total-value;
   differentialWeights[m]=value;differentialWeights[other]=total-value;
  }
  var stale=new List<KDAWheelMotor>();foreach(var m in differentialWeights.Keys)if(!active.Contains(m))stale.Add(m);foreach(var m in stale){differentialWeights.Remove(m);passiveDifferentialWeights.Remove(m);}
  UpdateAllocationReadouts();
 }
 void UpdateAwdClutch(double dt,KDACarControls controls,ref double wheelRpm,ref double axleLoad){
  awdHandbrakeClutch.Step(dt,ImportedAwdDriveline&&!BurnoutActive&&!BurnoutLaunching,controls!=null&&controls.HandbrakeActive);
  if(awdHandbrakeClutch.Coupling>=1)return;
  double weight=0,rpm=0,load=0,count=0;drivenCount=0;
  foreach(var m in motors)if(m.motorEnabled&&m.CanDrive){double coupling=DriveClutchForWheel(m);if(coupling<=0)continue;drivenCount++;weight+=m.DriveWeight*coupling;rpm+=m.WheelRpm*m.DriveRatio*m.DriveWeight*coupling;load+=m.EstimatedLoad*coupling;count+=coupling;}
  // Keep the existing normalization below unchanged outside this AWD branch.
  wheelRpm=weight>0?rpm/weight*driveWeights:0;axleLoad=count>0?load/count*drivenCount:0;
 }
 void ApplyAwdClutchAllocation(){
  if(awdHandbrakeClutch.Coupling>=1)return;double front=0,rear=0;
  foreach(var m in motors)if(m.motorEnabled&&m.CanDrive){double value;if(!differentialWeights.TryGetValue(m,out value))value=m.DriveWeight;if(part.transform.InverseTransformPoint(m.part.transform.position).y>0)front+=value;else rear+=value;}
  if(front<=0||rear<=0)return;
  var allocation=AwdHandbrakeClutch.Gate(front,0,rear,0,awdHandbrakeClutch.Coupling);
  foreach(var m in motors)if(m.motorEnabled&&m.CanDrive){double value;if(!differentialWeights.TryGetValue(m,out value))value=m.DriveWeight;differentialWeights[m]=value*(part.transform.InverseTransformPoint(m.part.transform.position).y>0?allocation.FL/front:allocation.RL/rear);}
  UpdateAllocationReadouts();
 }
 void LoadActiveDriveline(ConfigNode node){
  // Refresh immutable source capabilities even when an old craft is loaded.
  bool value;
  importedActiveCenter=bool.TryParse(node.GetValue("importedActiveCenter"),out value)&&value;
  importedFrontVectoring=bool.TryParse(node.GetValue("importedFrontVectoring"),out value)&&value;
  importedRearVectoring=bool.TryParse(node.GetValue("importedRearVectoring"),out value)&&value;
  drivelineHardware=ActiveCenterHardware?"Active AWD center":"Fixed axle split";
  if(FrontVectoringHardware)drivelineHardware+=" / front vectoring";
  if(RearVectoringHardware)drivelineHardware+=" / rear vectoring";
 }
 DrivelineWheelState ActiveWheel(KDAWheelMotor m){
  return new DrivelineWheelState{Weight=m.motorEnabled?m.DriveWeight:0,Grounded=m.DriveGrounded,Slip=m.LongitudinalSlip,ShaftSpeed=m.DriveAngularSpeed*finalDrive*m.DriveRatio,ExpectedShaftSpeed=m.DriveRoadAngularSpeed*finalDrive*m.DriveRatio};
 }
 bool TryActiveDifferential(double dt){
  if(BurnoutActive||BurnoutLaunching||part.rb==null)return false;
  KDAWheelMotor fl=null,fr=null,rl=null,rr=null;
  foreach(var m in motors){
   if(m.part.parent!=part||!m.CanDrive)return false;
   if(m.DriveWeight>0&&!m.motorEnabled)return false;
   Vector3 p=part.transform.InverseTransformPoint(m.part.transform.position);
   if(p.y>0){if(p.x<0){if(fl!=null)return false;fl=m;}else{if(fr!=null)return false;fr=m;}}
   else if(p.x<0){if(rl!=null)return false;rl=m;}else{if(rr!=null)return false;rr=m;}
  }
  if(fl==null||fr==null||rl==null||rr==null||driveWeights<=0)return false;
  var sc=part.FindModuleImplementing<KDAController>();var controls=part.FindModuleImplementing<KDACarControls>();
  bool powered=engineRunning&&gearbox.Selector!=0&&Math.Abs(pedal)>.01&&!(controls!=null&&controls.HandbrakeActive);
  double speed=Vector3.Dot((Vector3)vessel.srf_velocity,part.transform.up),yaw=Vector3.Dot(part.rb.angularVelocity,-part.transform.forward);
  double wheelbase=Math.Max(1,part.transform.InverseTransformPoint(fl.part.transform.position).y-part.transform.InverseTransformPoint(rl.part.transform.position).y);
  double command=Mathf.Clamp(vessel.ctrlState.wheelSteer+vessel.ctrlState.wheelSteerTrim,-1,1);
  double desired=sc!=null&&sc.stabilityEnabled&&sc.assistance>0?sc.DesiredYaw:-Math.Tan(command*Math.Max(fl.DriveSteeringRange,fr.DriveSteeringRange)*Math.PI/180)*Math.Max(0,speed)/wheelbase;
  double yawLimit=Math.Min(1.5,2.5*FlightGlobals.getGeeForceAtPosition(part.transform.position).magnitude/Math.Max(3,speed));
  desired=Math.Max(-yawLimit,Math.Min(yawLimit,desired));
  bool fullContact=fl.DriveGrounded&&fr.DriveGrounded&&rl.DriveGrounded&&rr.DriveGrounded;
  bool driftProfile=ImportedAwdDriveline&&sc!=null&&sc.driveMode==3&&sc.stabilityEnabled&&sc.assistance>0&&speed>=4&&fullContact&&awdHandbrakeClutch.Coupling>=1;
  double driftSlip=driftProfile?Math.Atan2(Vector3.Dot((Vector3)vessel.srf_velocity,part.transform.right),speed)*180/Math.PI:0;
  if(driftProfile)desired=AwdDriftDemand.YawTarget(yaw,sc.AwdRotationIntent,yawLimit);
  bool yawAllowed=powered&&speed>=4&&fullContact&&(sc==null||sc.driveMode!=3||driftProfile);
  var config=new ActiveDrivelineConfig{Center=ActiveCenterHardware,FrontVectoring=FrontVectoringHardware,RearVectoring=RearVectoringHardware,PassiveMode=differentialMode,CenterOffset=activeCenterOffset,CenterRate=activeCenterRate,SpeedThreshold=activeCenterSpeedThreshold,SpeedDifferenceThreshold=activeCenterDifferenceThreshold,FrontOffset=frontVectoringOffset,RearOffset=rearVectoringOffset,FrontRate=frontVectoringRate,RearRate=rearVectoringRate};
  if(sc!=null&&sc.stabilityEnabled&&ExtremeSpeedGovernor.Applies(importedVehicle,sc.driveMode,BurnoutActive||BurnoutLaunching,sc.assistance)){
   double weight=ExtremeSpeedGovernor.Weight(speed);
   config.FrontOffset=ExtremeSpeedGovernor.VectorOffset(config.FrontOffset,speed);config.RearOffset=ExtremeSpeedGovernor.VectorOffset(config.RearOffset,speed);
   desired=yaw+(desired-yaw)*(1-weight);if(weight>=1)yawAllowed=false;
  }
  if(driftProfile&&powered&&ActiveCenterHardware){double baseline=(rl.DriveWeight+rr.DriveWeight)/driveWeights;config.DriftRearMinimum=AwdDriftDemand.RearFloor(baseline,driftSlip,sc.AwdRotationIntent);}
  if(!driftProfile&&sc!=null&&sc.driveMode>=0&&sc.driveMode<=2&&sc.stabilityEnabled&&sc.assistance>0&&ImportedAwdDriveline&&powered&&fullContact&&speed>=12&&awdHandbrakeClutch.Coupling>=1&&ActiveCenterHardware&&!vessel.ActionGroups[KSPActionGroup.Brakes]&&vessel.ctrlState.wheelThrottle+vessel.ctrlState.wheelThrottleTrim>.05&&sc.AwdCorneringRearAssist>0){
   double baseline=(rl.DriveWeight+rr.DriveWeight)/driveWeights;config.PoweredRearMinimum=baseline+sc.AwdCorneringRearAssist;
  }
  ActiveYawTarget=(float)desired;DriftRearMinimum=(float)config.DriftRearMinimum;PoweredRearMinimum=(float)config.PoweredRearMinimum;
  var a=activeDriveline.Step(dt,config,ActiveWheel(fl),ActiveWheel(fr),ActiveWheel(rl),ActiveWheel(rr),desired,yaw,powered,yawAllowed);
  differentialWeights.Clear();differentialWeights[fl]=a.FL*driveWeights;differentialWeights[fr]=a.FR*driveWeights;differentialWeights[rl]=a.RL*driveWeights;differentialWeights[rr]=a.RR*driveWeights;
  rearDriveFraction=(float)a.RearShare;frontLeftDriveFraction=(float)a.FrontLeftShare;rearLeftDriveFraction=(float)a.RearLeftShare;vectorYawError=(float)a.YawError;return true;
 }
 public double DriveFractionForWheel(KDAWheelMotor m){double weight;return m!=null&&m.motorEnabled&&m.CanDrive&&driveWeights>0?(differentialWeights.TryGetValue(m,out weight)?weight:m.DriveWeight)/driveWeights:0;}
 void UpdateAllocationReadouts(){
  double fl=0,fr=0,rl=0,rr=0;
  foreach(var m in motors){double share=DriveFractionForWheel(m);Vector3 p=part.transform.InverseTransformPoint(m.part.transform.position);if(p.y>0){if(p.x<0)fl+=share;else fr+=share;}else if(p.x<0)rl+=share;else rr+=share;}
  rearDriveFraction=(float)(rl+rr);frontLeftDriveFraction=(float)(fl+fr>0?fl/(fl+fr):.5);rearLeftDriveFraction=(float)(rl+rr>0?rl/(rl+rr):.5);
 }
 public int ShiftSerial {get{return gearbox.ShiftSerial;}}
 public bool BurnoutActive {get{return burnout!=null&&burnout.Active;}}
 public bool BurnoutLaunching {get{return burnout!=null&&burnout.Launching;}}
 public KDABurnout DrivingEffects {get{return burnout;}}
 public void RestoreBurnoutGear(int gear){gearbox.Gear=Math.Max(1,Math.Min(gearbox.Ratios.Length,gear));selectedGear=gearbox.Gear;gearbox.Automatic=automaticShifting;}
 public float DragPerWheel(double rpm,bool enabled){
  EnsureStep();double peak=190;foreach(double torque in core.CurveTorque)peak=Math.Max(peak,torque);
  // Use the unboosted source curve as a bounded engine-size proxy. Boost and
  // TCS cuts must not increase closed-throttle losses or imitate a pedal lift.
  double scale=Math.Sqrt(peak/190)*Mathf.Clamp(engineBrakingScale,0,2);
  return (float)TuningCore.Drag(rpm,gearbox.Ratio,core.FinalDrive,core.LimitRpm,core.IdleRpm,engineRunning?Math.Abs(pedal):0,enabled&&gearbox.Selector!=0&&!gearbox.Shifting,drivenCount,Math.Min(2.3,scale));
 }
 public float AudioThrottle {get{return (float)core.Throttle;}}
 public bool IsShifting {get{return gearbox.Shifting;}}
 public bool AtRevLimiter {get{return core.Limiter;}}
 public bool LimiterEngaged {get{return core.LimiterActive;}}
 public float TurboRotor {get{return (float)core.Rotor;}}
 public float EstimatedEngineLoad {get{return (float)core.EngineLoad;}}
 public bool ProducingPower {get{return engineRunning&&!core.Limiter&&gearbox.Selector!=0;}}
 public bool AllowsDriveInput {get{float input=vessel.ctrlState.wheelThrottle+vessel.ctrlState.wheelThrottleTrim;return BurnoutActive||(gearbox.Selector>0?input>0:gearbox.Selector<0&&input<0);}}
 public float HudMaximumBoost {get{return maxBoostBar;}}
 public override string GetInfo(){return "Editor tuning: 4-6 gears, spacing/top speed, Automatic/Manual/Sequential, engine internals, RPM limit and induction.\nEngine ON, Drive + W; Reverse + S. Gear/RPM/boost in the body menu.";}
}

public class KDAWheelMotor : ModuleWheelMotor {
 [KSPField(isPersistant=true)] public float driveWeight=1,driveRatio=1;
 public double DriveWeight {get{return Mathf.Max(0,driveWeight);}}
 public double DriveRatio {get{return Mathf.Clamp(driveRatio,.1f,8);}}
 KDAEngine engine;
 readonly TractionCore traction=new TractionCore();
 readonly RoadSportTraction roadTraction=new RoadSportTraction();
 int tractionMode=2;
 [KSPField] public float roadSportPowerCap=1;
 public float TcsTorqueScale {get{return (float)(RoadSportCore.Applies(tractionMode)?roadTraction.Scale:traction.Scale);}}
 public override void OnSave(ConfigNode node){
  base.OnSave(node);if(vessel==null)return;
  foreach(Part p in vessel.parts){var effects=p.FindModuleImplementing<KDABurnout>();if(effects==null||!effects.Active)continue;
   bool enabled,auto;float traction,limiter;
   if(effects.OriginalMotor(this,out enabled,out auto,out traction,out limiter)){
    node.SetValue("motorEnabled",enabled.ToString(),true);node.SetValue("autoTorque",auto.ToString(),true);
    node.SetValue("tractionControlScale",traction.ToString(CultureInfo.InvariantCulture),true);node.SetValue("driveLimiter",limiter.ToString(CultureInfo.InvariantCulture),true);
   }
  }
 }
 public bool CanDrive {get{return baseSetup && wheelBase!=null && wheelBase.Wheel!=null
  && !wheelBase.InopSystems.HasType(WheelSubsystem.SystemTypes.Motor|WheelSubsystem.SystemTypes.WheelCollider|WheelSubsystem.SystemTypes.Tire);}}
 public double WheelRpm {get{return CanDrive?Math.Abs(wheelBase.Wheel.currentState.angularVelocity)*60/(2*Math.PI):0;}}
 public bool DriveGrounded {get{return CanDrive&&wheelBase.Wheel.currentState!=null&&wheelBase.Wheel.IsGrounded;}}
 public double DriveAngularSpeed {get{return CanDrive&&wheelBase.Wheel.currentState!=null?Math.Abs(wheelBase.Wheel.currentState.angularVelocity):0;}}
 public double DriveRoadAngularSpeed {get{return CanDrive&&wheelBase.Wheel.currentState!=null?Math.Abs(wheelBase.Wheel.currentState.localWheelVelocity.y)/Math.Max(.05,wheelBase.Wheel.WheelRadius):0;}}
 public double DriveSteeringRange {get{return CanDrive?Math.Abs(wheelBase.Wheel.maxSteerAngle):0;}}
 public double LongitudinalSlip {get{if(!CanDrive)return 0;var w=wheelBase.Wheel;double tread=Math.Abs(w.currentState.angularVelocity*w.WheelRadius),road=Math.Abs(w.currentState.localWheelVelocity.y);return Math.Max(0,tread-road)/Math.Max(4,road);}}
 public double EstimatedLoad {get{
  if(!CanDrive||!wheelBase.Wheel.IsGrounded)return .03;
  var w=wheelBase.Wheel;double tread=Math.Abs(w.currentState.angularVelocity*w.WheelRadius);
  double road=Math.Abs(w.currentState.localWheelVelocity.y);
  return EngineCore.Clamp(1-.35*Math.Abs(tread-road)/System.Math.Max(4,tread),.65,1);
 }}
 protected override float OnDriveUpdate(float input) {
  if(engine==null || engine.vessel!=vessel) {
   engine=null;
   foreach(Part p in vessel.parts){engine=p.FindModuleImplementing<KDAEngine>();if(engine!=null)break;}
  }
  float torque=engine!=null?engine.TorqueForWheel(this):0;
  var controls=engine==null?null:engine.part.FindModuleImplementing<KDACarControls>();
  var sc=engine==null?null:engine.part.FindModuleImplementing<KDAController>();
  bool tcs=controls!=null&&controls.TcsActive&&!engine.BurnoutActive&&CanDrive;
  float scale=(float)traction.Step(Time.fixedDeltaTime,tcs,LongitudinalSlip,CanDrive&&wheelBase.Wheel.IsGrounded,sc==null?2:sc.driveMode);
  tractionMode=sc==null?2:sc.driveMode;
  if(RoadSportCore.Applies(tractionMode))scale=(float)roadTraction.Step(Time.fixedDeltaTime,tcs,LongitudinalSlip,CanDrive&&wheelBase.Wheel.IsGrounded,tractionMode);
  else roadTraction.Reset();
  roadSportPowerCap=1;
  bool roadProfile=sc!=null&&RoadSportCore.Applies(sc.driveMode)&&sc.stabilityEnabled&&sc.assistance>0&&engine!=null&&!engine.BurnoutActive&&!engine.BurnoutLaunching;
  if(roadProfile){
   roadSportPowerCap=(float)RoadSportCore.PowerCap(sc.driveMode,true,controls!=null&&controls.TcsActive);
   torque*=roadSportPowerCap;
   input=(float)RoadSportCore.PedalInput(input,engine.RoadSportPedal,sc.driveMode,true);
  }
  torque*=engine!=null&&engine.BurnoutLaunching?Mathf.Lerp(1,scale,engine.DrivingEffects.LaunchTcsBlend):scale;
  if(wheelBase!=null && wheelBase.Wheel!=null)wheelBase.Wheel.maxDriveTorque=torque;
  if(engine!=null&&engine.BurnoutActive)return engine.ProducingPower?GetMotorOrientationSign()*(motorInverted?-1:1):0;
  return engine!=null&&engine.ProducingPower&&engine.AllowsDriveInput?input:0;
 }
}

// Stock steering derives its axes from the PART, so normalize the mounting
// quaternion of the entire part rather than altering only animated child frames.
public class KDAMount : PartModule {
 // PartModule.OnUpdate is a flight callback, not an editor update callback.
 // Align before saving in the editor, and never rotate a launched physics part.
 void Update(){if(HighLogic.LoadedSceneIsEditor)AlignEditor();}
 void AlignEditor(){
  if(part==null || part.parent==null || part.parent.partInfo==null || part.parent.FindModuleImplementing<KDAEngine>()==null)return;
  Transform body=part.parent.transform;
  bool left=body.InverseTransformPoint(part.transform.position).x<0;
  var suspension=part.FindModuleImplementing<KDASuspension>();
  float toe=suspension!=null?Mathf.Clamp(suspension.toeIn,-.5f,.5f):0;
  Quaternion rotation=body.rotation*Quaternion.Euler(0,0,left?-toe:toe)*Quaternion.Euler(-90,0,0)*(left?Quaternion.Euler(0,180,0):Quaternion.identity);
  if(Quaternion.Angle(part.transform.rotation,rotation)<.01f)return;
  part.transform.rotation=rotation;
  part.attRotation=Quaternion.Inverse(body.rotation)*rotation;
  part.attRotation0=part.attRotation;
 }
 public override string GetInfo(){return "Automatically aligns the complete wheel when attached to the vehicle body. No manual tire rotation needed.";}
}

public class KDAGrip : PartModule {
 public float BurnoutScale=1;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Tire grip multiplier",guiFormat="F2")]
 [UI_FloatRange(minValue=.4f,maxValue=3f,stepIncrement=.1f)] public float gripMultiplier=1f;
 ModuleWheelBase wheelBase;
 float adherent,peak,limit,lastGrip=-1;
 KSPWheelController appliedController;
 public override void OnStart(StartState state) {
  base.OnStart(state);wheelBase=part.FindModuleImplementing<ModuleWheelBase>();
  if(wheelBase!=null){
   wheelBase.autoFriction=false;wheelBase.frictionMultiplier=1;
   foreach(string name in new[]{"frictionMultiplier","autoFriction"}){var field=wheelBase.Fields[name];if(field!=null){field.guiActiveEditor=false;field.guiActive=false;}}
   var evt=wheelBase.Events["EvtAutoFrictionToggle"];if(evt!=null){evt.guiActiveEditor=false;evt.guiActive=false;}
   adherent=wheelBase.frictionAdherent;peak=wheelBase.frictionPeak;limit=wheelBase.frictionLimit;
   // Editor cloning can copy already-scaled fields. Always derive from prefab config.
   if(part.partInfo!=null && part.partInfo.partConfig!=null)
    foreach(ConfigNode node in part.partInfo.partConfig.GetNodes("MODULE"))
     if(node.GetValue("name")=="ModuleWheelBase") {
      adherent=ReadCoefficient(node,"frictionAdherent",adherent);
      peak=ReadCoefficient(node,"frictionPeak",peak);limit=ReadCoefficient(node,"frictionLimit",limit);
     }
   lastGrip=-1;Apply();
  }
 }
 static float ReadCoefficient(ConfigNode node,string key,float fallback){
  float value;return float.TryParse(node.GetValue(key),NumberStyles.Float,CultureInfo.InvariantCulture,out value)
   && !float.IsNaN(value) && !float.IsInfinity(value) && value>=0?value:fallback;
 }
 public override void OnUpdate(){Apply();}
 void Apply(){
  if(wheelBase==null)return;
  gripMultiplier=Mathf.Clamp(gripMultiplier,.4f,3f);
  // A displayed 1.00 should use the imported tire coefficients unchanged.
  float effective=gripMultiplier*Mathf.Clamp(BurnoutScale,.2f,1.5f);
  bool ready=HighLogic.LoadedSceneIsFlight&&wheelBase.Wheel!=null;
  if(Mathf.Approximately(lastGrip,effective)&&(!ready||(appliedController==wheelBase.Wheel&&wheelBase.Wheel.tireFriction.model==VehiclePhysics.TireFriction.Model.Smooth)))return;
  wheelBase.frictionAdherent=adherent*effective;wheelBase.frictionPeak=peak*effective;
  wheelBase.frictionLimit=limit*effective;lastGrip=effective;
  if(HighLogic.LoadedSceneIsFlight && wheelBase.Wheel!=null){
   appliedController=wheelBase.Wheel;
   var friction=wheelBase.Wheel.tireFriction;
   friction.model=VehiclePhysics.TireFriction.Model.Smooth;
   friction.settings.adherent=new Vector2(wheelBase.adherentStart,wheelBase.frictionAdherent*wheelBase.frictionMultiplier);
   friction.settings.peak=new Vector2(wheelBase.peakStart,wheelBase.frictionPeak*wheelBase.frictionMultiplier);
   friction.settings.limit=new Vector2(wheelBase.limitStart,wheelBase.frictionLimit*wheelBase.frictionMultiplier);
   friction.SetupFrictionCurves();
   wheelBase.UpdateFriction();
  }
 }
 public override string GetInfo(){return "Adjust tire grip in the editor (0.40–3.00). Default 1.00. Wheel setup is editor-only.";}
}
