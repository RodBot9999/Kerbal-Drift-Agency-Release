using System;
using System.Collections.Generic;
using ModuleWheels;
using UnityEngine;

public class KDAController:PartModule {
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Chassis preset")]
 [UI_ChooseOption(options=new[]{"Road","Sport","Race","Drift"})] public int chassisMode=1;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="SC mode")]
 [UI_ChooseOption(options=new[]{"Road","Sport","Race","Drift"})] public int driveMode=1;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Drift steering lock (deg)",guiFormat="F0")]
 [UI_FloatRange(minValue=25,maxValue=75,stepIncrement=1)] public float driftSteeringLock=50;
 public void SetDriftSteeringLock(float degrees){driftSteeringLock=(float)KDACore.DriftLock(degrees);}
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Stability control (SC)")] public bool stabilityEnabled=true;
 [KSPAction("Toggle stability control")] public void ToggleSc(KSPActionParam p){stabilityEnabled=!stabilityEnabled;}
 [KSPEvent(guiActive=true,guiActiveEditor=true,guiName="SC ENABLE")] public void EnableSc(){stabilityEnabled=true;}
 [KSPEvent(guiActive=true,guiActiveEditor=true,guiName="SC DISABLE")] public void DisableSc(){stabilityEnabled=false;}
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="SC assistance",guiFormat="P0")]
 [UI_FloatRange(minValue=0,maxValue=1,stepIncrement=.05f)] public float assistance=1;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Allowed oversteer (deg)",guiFormat="F0")]
 [UI_FloatRange(minValue=0,maxValue=18,stepIncrement=1)] public float allowedOversteer=5;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Cornering allowance +/- (%)",guiFormat="F0")]
 [UI_FloatRange(minValue=-40,maxValue=60,stepIncrement=5)] public float corneringAdjustmentPercent=0;
 [KSPField(guiActive=true,guiName="SC cornering budget",guiUnits=" g",guiFormat="F2")] public float corneringBudgetG;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Stability throttle aid",guiFormat="P0")]
 [UI_FloatRange(minValue=0,maxValue=1,stepIncrement=.05f)] public float throttleAid=1;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Keyboard steering smoothing (s)",guiFormat="F2")]
 [UI_FloatRange(minValue=.05f,maxValue=.35f,stepIncrement=.01f)] public float keyboardSmoothing=.16f;
 [KSPField(guiActive=true,guiName="SC status")] public string status="Ready";
 float throttleScale=1;
 public float ThrottleScale {get{var controls=part==null?null:part.FindModuleImplementing<KDACarControls>();return stabilityEnabled&&controls!=null&&controls.TcsActive?throttleScale:1;}private set{throttleScale=value;}}
 public float SteeringLimit {get;private set;}
 public float SteeringCorrection {get;private set;}
 public float RawSteering {get;private set;}
 public float LastRecoveryLimit {get;private set;}
 public double DesiredYaw {get;private set;}
 public bool ObserverActive {get{return observer.Active;}}
 public double ObservedSlip {get{return observer.Slip;}}
 public double ObservedYaw {get{return observer.Yaw;}}
 public double ObservedSlipRate {get{return observer.SlipRate;}}
 public double ObservedYawRate {get{return observer.YawRate;}}
 public double RaceRecoveryBias {get{return raceKeyboard.Bias;}}
 [KSPField] public float raceSettlingWeight;
 [KSPField] public float raceTurnInScale=1;
 public float DriverSteering {get{return driverSteer;}}
 public float YawBrakeDemand {get{return yawBrake;}}
 public double DriftBias {get{return driftKeyboard.Bias;}}
 public bool RecoveryActive {get{return stabilityEnabled&&hasOutput&&(extremeGovernorActive||observer.Active||driveMode==3&&(driftIntent.Active||driftKeyboard.Blending)||driveMode==2&&raceKeyboard.Blending||RoadSportCore.Applies(driveMode)&&roadKeyboard.Blending);}}
 public float RecoveryCommand {get{return outputSteer;}}
 public string ModeName {get{return driveMode==3?"Drift":driveMode==2?"Race":driveMode==1?"Sport":"Road";}}
 Vessel subscribed;KDAEngine engine;int previousPreset=-1;float outputSteer,driverSteer,filteredThrottle=1;bool hasOutput,extremeGovernorActive;
 struct NativeSteering {public bool Auto;public float Response,Angle;}
 readonly Dictionary<ModuleWheelSteering,NativeSteering> originalSteering=new Dictionary<ModuleWheelSteering,NativeSteering>();
 readonly StabilityObserver observer=new StabilityObserver();
 readonly DriftSteeringIntent driftIntent=new DriftSteeringIntent();
 readonly DriftKeyboardBlend driftKeyboard=new DriftKeyboardBlend();
 readonly KerbalDriftAgency.AwdDriftTrim awdDriftTrim=new KerbalDriftAgency.AwdDriftTrim();
 public double AwdRotationIntent {get{return awdDriftTrim.RotationIntent;}}
 public double AwdAddedSteering {get{return awdDriftTrim.AddedSteering;}}
 readonly KerbalDriftAgency.AwdCorneringCore awdCornering=new KerbalDriftAgency.AwdCorneringCore();
 public double AwdCorneringSteer {get{return awdCornering.AddedSteering;}}
 public double AwdCorneringYawDeficit {get{return awdCornering.YawDeficit;}}
 public double AwdCorneringRearAssist {get{return awdCornering.RearAssist;}}
 public double AwdCorneringSupport {get{return awdCornering.Support;}}
 readonly RaceRecoveryBlend raceKeyboard=new RaceRecoveryBlend();
 readonly RaceNeutralSettling raceSettling=new RaceNeutralSettling();
 readonly RaceCorneringGuard raceCornering=new RaceCorneringGuard();
 readonly RaceRecoveryBlend roadKeyboard=new RaceRecoveryBlend();
 readonly RaceNeutralSettling roadSettling=new RaceNeutralSettling();
 readonly RaceCorneringGuard roadCornering=new RaceCorneringGuard();
 [KSPField] public float roadSportTurnScale=1;
 [KSPField] public float roadSportSettlingWeight;
 [KSPField] public float roadSportRecoveryBias;
 void ResetRoad(){roadKeyboard.Reset();roadSettling.Reset();roadCornering.Reset();roadSportTurnScale=1;roadSportSettlingWeight=roadSportRecoveryBias=0;}
 float yawBrake;
 public float StabilityBrake(Part wheel){
  if(!stabilityEnabled||assistance<=0||engine!=null&&(engine.BurnoutActive||engine.BurnoutLaunching))return 0;
  Vector3 pos=part.transform.InverseTransformPoint(wheel.transform.position);
  return (float)DriverAidsCore.YawBrake(yawBrake,pos.x,pos.y>0,RoadSportCore.Applies(driveMode)?2:driveMode);
 }
 public override void OnStart(StartState s){base.OnStart(s);engine=part.FindModuleImplementing<KDAEngine>();ThrottleScale=SteeringLimit=1;previousPreset=chassisMode;Subscribe();}
 void Subscribe(){if(!HighLogic.LoadedSceneIsFlight||vessel==subscribed)return;if(subscribed!=null)subscribed.OnFlyByWire-=Drive;subscribed=vessel;if(subscribed!=null)subscribed.OnFlyByWire+=Drive;}
 void Update(){Subscribe();SetDriftSteeringLock(driftSteeringLock);Fields["driftSteeringLock"].guiActive=Fields["driftSteeringLock"].guiActiveEditor=driveMode==3;Fields["corneringBudgetG"].guiActive=stabilityEnabled&&assistance>0;if(HighLogic.LoadedSceneIsEditor&&previousPreset!=chassisMode){previousPreset=chassisMode;ApplyPreset();}}
 [KSPEvent(guiActiveEditor=true,guiName="Apply chassis preset to all tires")]
 public void ApplyPreset(){
  if(!HighLogic.LoadedSceneIsEditor||EditorLogic.fetch==null)return;
  var tune=KDACore.Preset(chassisMode);
  foreach(Part p in EditorLogic.fetch.ship.parts){
   if(p.parent!=part)continue;var s=p.FindModuleImplementing<KDASuspension>();if(s==null)continue;
   bool front=part.transform.InverseTransformPoint(p.transform.position).y>0;
   s.suspensionKit=0;s.AcceptGarageSetup();
   s.springRateNmm=(float)(front?tune.FrontSpring:tune.RearSpring);s.rideHeightMm=(float)tune.Height;
   s.antiRollRate=(float)(front?tune.FrontBar:tune.RearBar);s.bumpDamping=(float)tune.Bump;s.reboundDamping=(float)tune.Rebound;s.fastBumpDamping=(float)tune.FastBump;s.fastReboundDamping=(float)tune.FastRebound;
   s.toeIn=front?0:chassisMode==3?.05f:.15f;s.axleInitialized=true;
   s.suspensionVersion=2;
   var grip=p.FindModuleImplementing<KDAGrip>();if(grip!=null)grip.gripMultiplier=(float)tune.Grip;
  }
  // Capture after all writes so linking cannot restore an older axle setting.
  foreach(Part p in EditorLogic.fetch.ship.parts)if(p.parent==part){var link=p.FindModuleImplementing<KDAAxle>();if(link!=null)link.Capture();}
 }
 public void SelectDriveMode(int mode){driveMode=Mathf.Clamp(mode,0,3);var aids=part.FindModuleImplementing<KDACarControls>();if(aids!=null)aids.Apply();}
 [KSPEvent(guiActive=true,guiName="Next driving mode")]
 public void NextDriveMode(){SelectDriveMode((Mathf.Clamp(driveMode,0,3)+1)%4);}
 [KSPAction("Next driving mode")] public void NextDriveModeAction(KSPActionParam p){NextDriveMode();}
 [KSPAction("Previous driving mode")] public void PreviousDriveModeAction(KSPActionParam p){SelectDriveMode((Mathf.Clamp(driveMode,0,3)+3)%4);}
 public void Drive(FlightCtrlState state){
  extremeGovernorActive=false;
  float previousAwdCornering=(float)awdCornering.AddedSteering;
  if(driveMode!=3||engine==null||!engine.ImportedAwdDriveline)awdDriftTrim.Reset();
  if(driveMode==3||engine==null||!engine.ImportedAwdDriveline)awdCornering.Reset();
  RawSteering=Mathf.Clamp(state.wheelSteer+state.wheelSteerTrim,-1,1);DesiredYaw=0;LastRecoveryLimit=1;raceSettlingWeight=0;raceTurnInScale=1;
  float assist=stabilityEnabled?assistance:0;
  bool roadMode=RoadSportCore.Applies(driveMode);
  if(!roadMode)ResetRoad();
  bool imported=engine!=null&&engine.importedVehicle;
  ThrottleScale=SteeringLimit=1;SteeringCorrection=0;yawBrake=0;
  if(vessel==null||vessel.packed||vessel!=FlightGlobals.ActiveVessel||part.rb==null){awdCornering.Reset();status="Standby";hasOutput=false;observer.Reset();driftIntent.Reset();driftKeyboard.Reset();raceKeyboard.Reset();raceSettling.Reset();raceCornering.Reset();ResetRoad();return;}
  Vector3 up=-part.transform.forward,forward=part.transform.up,right=part.transform.right;
  Vector3 velocity=(Vector3)vessel.srf_velocity;float longitudinal=Vector3.Dot(velocity,forward),lateral=Vector3.Dot(velocity,right);
  double awdFrontSide=0,awdRearSide=0,awdFrontSpin=0,awdRearSpin=0;
  int grounded=0,count=0,frontTires=0;float frontPeak=0,left=0,rightLoad=0,frontY=-100,rearY=100,maxAngle=0;Vector3 normal=Vector3.zero;
  foreach(Part p in vessel.parts){if(p.parent!=part)continue;var b=p.FindModuleImplementing<ModuleWheelBase>();if(b==null||b.Wheel==null)continue;var w=b.Wheel;count++;
   Vector3 pos=part.transform.InverseTransformPoint(p.transform.position);frontY=Mathf.Max(frontY,pos.y);rearY=Mathf.Min(rearY,pos.y);
   if(pos.y>0){frontPeak+=b.frictionPeak*b.frictionMultiplier;frontTires++;}
   if(engine!=null&&engine.ImportedAwdDriveline&&driveMode!=3&&w.currentState!=null){
    double side=Math.Abs(w.currentState.localWheelVelocity.x)/Math.Max(.1,b.peakStart);var motor=p.FindModuleImplementing<KDAWheelMotor>();double spin=motor==null?0:motor.LongitudinalSlip;
    if(pos.y>0){awdFrontSide=Math.Max(awdFrontSide,side);awdFrontSpin=Math.Max(awdFrontSpin,spin);}else {awdRearSide=Math.Max(awdRearSide,side);awdRearSpin=Math.Max(awdRearSpin,spin);}
   }
   var steering=p.FindModuleImplementing<ModuleWheelSteering>();if(steering!=null&&steering.steeringEnabled){
    var actuator=p.gameObject.GetComponent<KDARecoverySteering>();if(actuator==null)actuator=p.gameObject.AddComponent<KDARecoverySteering>();actuator.Controller=this;actuator.Steering=steering;actuator.Wheel=w;
    if(!originalSteering.ContainsKey(steering))originalSteering.Add(steering,new NativeSteering{Auto=steering.autoSteeringAdjust,Response=steering.responseTweakable,Angle=Mathf.Max(40,steering.angleTweakable)});
    steering.angleTweakable=assist>0&&driveMode==3?Mathf.Max(60,(float)KDACore.DriftLock(driftSteeringLock)+15):originalSteering[steering].Angle;steering.steeringRange=steering.angleTweakable;w.maxSteerAngle=steering.angleTweakable;
    if(assist>0||imported){steering.autoSteeringAdjust=false;steering.responseTweakable=10;}
    else {var original=originalSteering[steering];steering.autoSteeringAdjust=original.Auto;steering.responseTweakable=original.Response;}
    maxAngle=Mathf.Max(maxAngle,Mathf.Abs(w.maxSteerAngle));
   }
   if(!w.IsGrounded)continue;grounded++;normal+=w.currentState.hit.normal;float load=Mathf.Max(0,w.currentState.downforce);if(pos.x<0)left+=load;else rightLoad+=load;
  }
  float raw=Mathf.Clamp(state.wheelSteer+state.wheelSteerTrim,-1,1);
  bool intentional=engine!=null&&(engine.BurnoutActive||engine.BurnoutLaunching);
  if(assist<=0||grounded<2||count<4||maxAngle<.01f){
   awdDriftTrim.Reset();awdCornering.Reset();
   status=grounded<2?"Airborne / standby":assist<=0?"Disabled":"Standby";filteredThrottle=1;observer.Reset();driftIntent.Reset();driftKeyboard.Reset();raceKeyboard.Reset();raceSettling.Reset();raceCornering.Reset();ResetRoad();
   if(ExtremeSpeedGovernor.Applies(imported,driveMode,intentional,assist)&&grounded<2&&count>=4&&maxAngle>.01f&&velocity.magnitude>65){
    float limit=(float)ExtremeSpeedGovernor.Limit(velocity.magnitude,maxAngle,frontY-rearY,FlightGlobals.getGeeForceAtPosition(part.transform.position).magnitude,corneringBudgetG>0?corneringBudgetG:.95);
    outputSteer=driverSteer=Mathf.Clamp(raw,-limit,limit);SteeringLimit=LastRecoveryLimit=limit;
    state.wheelSteer=outputSteer-state.wheelSteerTrim;hasOutput=extremeGovernorActive=true;status="Airborne / bounded steering";
   }else if(imported&&assist<=0&&grounded>=2){
    if(!hasOutput)driverSteer=0;
    float disabledTau=(float)KDACore.SteeringTau(driverSteer,raw,keyboardSmoothing,driveMode);
    driverSteer=Mathf.Lerp(driverSteer,raw,1-Mathf.Exp(-Time.fixedDeltaTime/disabledTau));
    state.wheelSteer=driverSteer-state.wheelSteerTrim;hasOutput=true;
   }else hasOutput=false;
   return;
  }
  float slip=Mathf.Atan2(lateral,longitudinal)*Mathf.Rad2Deg;
  float roll=normal.sqrMagnitude>.01f?Vector3.Angle(up,normal.normalized):0;
  float unloading=Mathf.Abs(left-rightLoad)/Mathf.Max(.01f,left+rightLoad);
  float gravity=(float)FlightGlobals.getGeeForceAtPosition(part.transform.position).magnitude;
  var aero=imported?part.FindModuleImplementing<KDAAero>():null;
  double capacity=imported?KDACore.ImportedCapacity(frontTires>0?frontPeak/frontTires:1.15,aero==null?0:aero.downforceN,vessel.totalMass*1000,gravity):1;
  capacity=KDACore.CorneringScale(imported,driveMode,capacity,corneringAdjustmentPercent);
  corneringBudgetG=(float)((driveMode>=2?.95:driveMode==1?.70:.48)*capacity);
  // Race now honors the selected tolerance without an extra hidden 3 degrees.
  float tolerance=allowedOversteer+(driveMode==0?-2:driveMode==3?7:0);
  if(roadMode)tolerance=(float)RoadSportCore.Tolerance(driveMode,allowedOversteer);
  double settling=raceSettling.Step(Time.fixedDeltaTime,driveMode==2&&!intentional&&longitudinal>=12,raw,slip,Vector3.Dot(part.rb.angularVelocity,up),observer.Active);
  raceSettlingWeight=(float)settling;
  if(roadMode){settling=roadSettling.Step(Time.fixedDeltaTime,!intentional&&longitudinal>=8,raw,slip,Vector3.Dot(part.rb.angularVelocity,up),observer.Active);roadSportSettlingWeight=(float)settling;}
  tolerance=Mathf.Lerp(tolerance,Mathf.Min(tolerance,1.5f),(float)settling);
  var result=roadMode?RoadSportCore.Evaluate(driveMode,assist,tolerance,longitudinal,raw,maxAngle,frontY-rearY,gravity,slip,Vector3.Dot(part.rb.angularVelocity,up),roll,unloading,true,intentional,capacity):KDACore.Evaluate(driveMode,assist,tolerance,longitudinal,raw,maxAngle,frontY-rearY,gravity,slip,Vector3.Dot(part.rb.angularVelocity,up),roll,unloading,true,intentional,capacity,driftSteeringLock);
  SteeringLimit=(float)result.Limit;SteeringCorrection=(float)result.Correction;
  // Native steering servo stays fast. Smooth the driver's limited demand,
  // then add the faster aid so keyboard ramps do not delay countersteering.
  if(observer.Active)result.RecoveryLimit=Math.Max(result.Limit,roadMode?RoadSportCore.RecoveryLock(maxAngle,assist):KDACore.RecoveryLock(driveMode,maxAngle,assist,driftSteeringLock));
  if(intentional||longitudinal<4)driftIntent.Reset();
  else driftIntent.Step(Time.fixedDeltaTime,driveMode,observer.Active);
  float driverLimit=(float)KDACore.DriverLimit(raw,observer.Slip,observer.Yaw,observer.Active,result);
  // Race recovery keys are a progressive trim below, never a full-lock step.
  if(driveMode==2||roadMode)driverLimit=(float)result.Limit;
  // Drift keys trim the balancing angle below; they no longer demand full
  // recovery lock or suppress the controller's countersteering.
  if(driveMode==3&&(driftIntent.Active||driftKeyboard.Blending))driverLimit=(float)result.Limit;
  float demand=Mathf.Clamp(raw,-driverLimit,driverLimit);
  double turnScale=raceCornering.Step(Time.fixedDeltaTime,driveMode==2&&!intentional&&longitudinal>=12,raw,slip,observer.SlipRate,allowedOversteer);
  raceTurnInScale=(float)turnScale;
  if(roadMode){turnScale=roadCornering.Step(Time.fixedDeltaTime,!intentional&&longitudinal>=8,raw,slip,observer.SlipRate,tolerance);roadSportTurnScale=(float)turnScale;}
  if(driveMode==2||roadMode)demand*=Mathf.Lerp(1,(float)turnScale,assist);
  if(!hasOutput)driverSteer=0;
  float tau=(float)(roadMode?RoadSportCore.SteeringTau(driveMode,driverSteer,demand,keyboardSmoothing):KDACore.SteeringTau(driverSteer,demand,keyboardSmoothing,driveMode));
  driverSteer=Mathf.Lerp(driverSteer,demand,1-Mathf.Exp(-Time.fixedDeltaTime/tau));
  if(Mathf.Abs(raw)<.002f&&Mathf.Abs(driverSteer)<.0005f)driverSteer=0;
  float yawDemand=driveMode==2||roadMode?Mathf.Clamp(driverSteer,-SteeringLimit,SteeringLimit):driverSteer;
  double desiredYaw=-(driveMode==3?Math.Tan(yawDemand*maxAngle*Mathf.Deg2Rad):yawDemand*Math.Tan(maxAngle*Mathf.Deg2Rad))*Math.Max(0,longitudinal)/Math.Max(1,frontY-rearY);
  DesiredYaw=desiredYaw;
  if(intentional||longitudinal<4)observer.Reset();
  else observer.Step(Time.fixedDeltaTime,slip,Vector3.Dot(part.rb.angularVelocity,up),desiredYaw,tolerance);
  // Compare motion with the progressive demand, not the unsmoothed key step.
  float currentYaw=Vector3.Dot(part.rb.angularVelocity,up);
  result=roadMode?RoadSportCore.Evaluate(driveMode,assist,tolerance,longitudinal,driverSteer,maxAngle,frontY-rearY,gravity,observer.Slip,observer.Yaw,roll,unloading,true,intentional,capacity,observer.SlipRate,currentYaw,observer.YawRate,settling):KDACore.Evaluate(driveMode,assist,tolerance,longitudinal,driverSteer,maxAngle,frontY-rearY,gravity,observer.Slip,observer.Yaw,roll,unloading,true,intentional,capacity,driftSteeringLock,observer.SlipRate,currentYaw,observer.YawRate,settling);
  SteeringCorrection=observer.Active?(float)result.Correction:0;
  result.RecoveryLimit=observer.Active||driveMode==3&&driftIntent.Active?Math.Max(result.Limit,roadMode?RoadSportCore.RecoveryLock(maxAngle,assist):KDACore.RecoveryLock(driveMode,maxAngle,assist,driftSteeringLock)):result.Limit;
  LastRecoveryLimit=(float)result.RecoveryLimit;
  float ordinary=Mathf.Clamp(driverSteer+SteeringCorrection,-(float)result.RecoveryLimit,(float)result.RecoveryLimit);
  if(driveMode==2&&!intentional&&longitudinal>=4){
   driftKeyboard.Reset();
   float turn=Mathf.Clamp(driverSteer,-(float)result.Limit,(float)result.Limit);
   outputSteer=(float)raceKeyboard.Step(Time.fixedDeltaTime,observer.Active,raw,SteeringCorrection,turn,hasOutput?outputSteer-previousAwdCornering:turn,maxAngle,Math.Max(result.Limit,KDACore.RecoveryLock(2,maxAngle,assist)),slip,currentYaw,desiredYaw);
  }else if(driveMode==3&&!intentional){
   raceKeyboard.Reset();
   bool drifting=observer.Active||driftIntent.Active;
   float previousBase=hasOutput?outputSteer:ordinary;
   if(engine!=null&&engine.ImportedAwdDriveline&&hasOutput)previousBase-=(float)awdDriftTrim.AddedSteering;
   outputSteer=(float)driftKeyboard.Step(Time.fixedDeltaTime,drifting,raw,SteeringCorrection,KDACore.RecoveryLock(driveMode,maxAngle,assist,driftSteeringLock),ordinary,previousBase,15.0/maxAngle,velocity.magnitude);
   if(engine!=null&&engine.ImportedAwdDriveline)outputSteer=(float)awdDriftTrim.Step(Time.fixedDeltaTime,true,drifting,raw,SteeringCorrection,outputSteer,slip,longitudinal,maxAngle,KDACore.DriftLock(driftSteeringLock));
  }else if(roadMode&&!intentional&&longitudinal>=4){
   driftKeyboard.Reset();raceKeyboard.Reset();
   float turn=Mathf.Clamp(driverSteer,-(float)result.Limit,(float)result.Limit);
   outputSteer=(float)roadKeyboard.Step(Time.fixedDeltaTime,observer.Active,raw,SteeringCorrection,turn,hasOutput?outputSteer-previousAwdCornering:turn,maxAngle,Math.Max(result.Limit,RoadSportCore.RecoveryLock(maxAngle,assist)),slip,currentYaw,desiredYaw);
   roadSportRecoveryBias=(float)roadKeyboard.Bias;
  }else {driftKeyboard.Reset();raceKeyboard.Reset();if(roadMode)ResetRoad();outputSteer=ordinary;}
  var controls=part.FindModuleImplementing<KDACarControls>();
  bool poweredAwd=engine!=null&&engine.ImportedAwdDriveline&&engine.engineRunning&&engine.driveSelector>0&&engine.RearDrivelineCoupling>=1&&assist>0&&!intentional&&driveMode!=3&&grounded==4&&count==4&&Math.Abs(state.wheelThrottle+state.wheelThrottleTrim)>.05&&state.wheelThrottle+state.wheelThrottleTrim>0&&engine.RequestedThrottle>.05&&!vessel.ActionGroups[KSPActionGroup.Brakes]&&!(controls!=null&&controls.HandbrakeActive)&&!observer.Active&&(driveMode==2?!raceKeyboard.Blending&&raceTurnInScale>.98f:roadMode&&!roadKeyboard.Blending&&roadSportTurnScale>.98f);
  outputSteer=(float)awdCornering.Step(Time.fixedDeltaTime,poweredAwd,driveMode,assist,raw,outputSteer,desiredYaw,currentYaw,observer.YawRate,longitudinal,frontY-rearY,maxAngle,slip,observer.SlipRate,tolerance,unloading,roll,awdFrontSide,awdRearSide,awdFrontSpin,awdRearSpin);
  if(ExtremeSpeedGovernor.Applies(imported,driveMode,intentional,assist)&&ExtremeSpeedGovernor.Weight(longitudinal)>0){
   outputSteer=(float)ExtremeSpeedGovernor.Command(outputSteer,driverSteer,longitudinal,maxAngle,frontY-rearY,result.Limit,currentYaw,desiredYaw,observer.YawRate,assist);
   SteeringCorrection=outputSteer-driverSteer;extremeGovernorActive=true;
  }
  hasOutput=true;
  state.wheelSteer=outputSteer-state.wheelSteerTrim;
  bool cutEnabled=controls!=null&&controls.TcsActive;
  // Slides use steering and selective braking. A whole-engine lift is
  // reserved for severe roll, avoiding lift-off oversteer in normal drifts.
  yawBrake=observer.Active&&!intentional?(float)DriverAidsCore.ExcessYaw(observer.Yaw,desiredYaw):0;
  if(extremeGovernorActive)yawBrake*=1-(float)ExtremeSpeedGovernor.Weight(longitudinal);
  float safePower=(float)DriverAidsCore.RollPower(roll,driveMode);
  float targetThrottle=cutEnabled?Mathf.Lerp(1,safePower,Mathf.Clamp01(throttleAid)):1;
  filteredThrottle=cutEnabled?Mathf.MoveTowards(filteredThrottle,targetThrottle,Time.fixedDeltaTime*(targetThrottle<filteredThrottle?.5f:.7f)):1;ThrottleScale=filteredThrottle;
  bool recovery=observer.Active&&Mathf.Abs(SteeringCorrection*maxAngle)>.15f;
  bool limiting=Mathf.Abs(raw)>driverLimit+.02f;
  status=intentional?"Burnout / steering safety":Math.Abs(awdCornering.AddedSteering)*maxAngle>.03?"AWD "+ModeName+" / powered cornering":ThrottleScale<.98f?"Stability / throttle cut":driveMode==3&&driftKeyboard.Blending&&Mathf.Abs(raw)>.02f?"Drift / progressive steering":recovery?"SC / correcting slide":driveMode==2&&raceTurnInScale<.98f?"Race / balancing corner":limiting?"SC / cornering limit":"Monitoring";
 }
 void OnDestroy(){if(subscribed!=null)subscribed.OnFlyByWire-=Drive;}
}

// Execute after the stock module computes Ackermann steering. Ordinary turns
// retain that geometry. Slide recovery uses the controller's actual wheel-angle
// budget instead of letting the outer wheel lose corrective authority.
[DefaultExecutionOrder(1000)]
public sealed class KDARecoverySteering:MonoBehaviour {
 public KDAController Controller;public ModuleWheelSteering Steering;public KSPWheelController Wheel;
 float orientation;bool ownedKeyboard,originalKeyboard;
 void FixedUpdate(){
  if(Steering==null||Wheel==null||Controller==null)return;
  if(Mathf.Abs(Steering.steeringInput)>.0001f&&Mathf.Abs(Steering.steerAngle)>.001f)
   orientation=Mathf.Sign(Steering.steerAngle/Steering.steeringInput)*(Steering.steeringInvert?-1:1);
  bool active=HighLogic.LoadedSceneIsFlight&&!Steering.part.packed&&Steering.steeringEnabled&&Controller.RecoveryActive;
  if(!active){if(ownedKeyboard){Wheel.keyboardSteering=originalKeyboard;ownedKeyboard=false;}return;}
  if(orientation==0)return; // Learn the rig's sign from stock; never guess axes.
  if(!ownedKeyboard){originalKeyboard=Wheel.keyboardSteering;ownedKeyboard=true;}
  Wheel.keyboardSteering=false;
  Wheel.steerInput=Mathf.Clamp(Controller.RecoveryCommand,-1,1)*orientation;
  Wheel.steeringResponse=40;
 }
}

