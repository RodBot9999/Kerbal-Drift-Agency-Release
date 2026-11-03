using System;
using System.Collections.Generic;
using ModuleWheels;
using UnityEngine;
using KerbalDriftAgency;

public class KDABurnout : PartModule {
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Enable W+S burnout")] public bool burnoutEnabled=true;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Tire smoke")] public bool smokeEnabled=true;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Smoke density",guiFormat="F2")]
 [UI_FloatRange(minValue=0,maxValue=1,stepIncrement=.05f)] public float smokeDensity=.75f;
 public bool Active {get{return state.Active;}}
 public bool Launching {get;private set;}
 double launchFloor;
 float tcsBlend;
 public double LaunchFloor {get{return Launching?launchFloor:0;}}
 public float LaunchTcsBlend {get{return tcsBlend;}}
 // Input seam also permits other controllers to provide the two-pedal command.
 public Func<bool> ReadBothKeys;
 readonly BurnoutCore state=new BurnoutCore();
 sealed class Saved {public KDAWheelMotor motor;public KDAGrip grip;public bool enabled,auto;public float traction,limiter;}
 readonly List<Saved> saved=new List<Saved>();KDAEngine engine;int previousGear;
 public override void OnStart(StartState s){base.OnStart(s);engine=part.FindModuleImplementing<KDAEngine>();if(ReadBothKeys==null)ReadBothKeys=()=>Input.GetKey(KeyCode.W)&&Input.GetKey(KeyCode.S);}
 public void Evaluate(){
  if(engine==null)return;
  bool grounded=false;if(vessel!=null)foreach(Part p in vessel.parts){var b=p.FindModuleImplementing<ModuleWheelBase>();if(b!=null&&b.Wheel!=null&&b.Wheel.IsGrounded&&!IsFront(p)){grounded=true;break;}}
  bool was=Active;
  bool allowed=burnoutEnabled&&HighLogic.LoadedSceneIsFlight&&vessel!=null&&vessel==FlightGlobals.ActiveVessel&&!vessel.packed&&!FlightDriver.Pause&&Time.timeScale>0&&!MapView.MapIsEnabled&&!InputLockManager.IsLocked(ControlTypes.WHEEL_THROTTLE);
  state.Step(ReadBothKeys!=null&&ReadBothKeys(),allowed,engine.engineRunning,engine.driveSelector==1,grounded,vessel==null?0:vessel.srfSpeed);
  if(!was&&Active){Launching=false;Begin();}else if(was&&!Active){
   bool launch=allowed&&engine.engineRunning&&engine.driveSelector==1&&vessel.ctrlState.wheelThrottle>.1;
   launchFloor=engine.engineRpm;tcsBlend=0;Restore();Launching=launch;
   if(Launching)engine.RestoreBurnoutGear(1);
  }
  if(Launching){
   double slip=0;foreach(Part p in vessel.parts){var m=p.FindModuleImplementing<KDAWheelMotor>();if(m!=null&&m.motorEnabled)slip=Math.Max(slip,m.LongitudinalSlip);}
   if(!allowed||!engine.engineRunning||engine.driveSelector!=1||vessel.ctrlState.wheelThrottle<=.1||!grounded||slip<.12){Launching=false;launchFloor=0;}
   else{
    // Relax clutch floor as road speed catches up; no fixed wheelspin duration.
    launchFloor=Math.Min(launchFloor,engine.engineRpm);
    launchFloor=Math.Max(engine.launchRpm,launchFloor-TimeWarp.fixedDeltaTime*900/(1+slip*3));
    tcsBlend=Mathf.MoveTowards(tcsBlend,1,TimeWarp.fixedDeltaTime*3);
   }
  }
 }
 void Begin(){
  previousGear=engine.selectedGear;
  foreach(Part p in vessel.parts){var motor=p.FindModuleImplementing<KDAWheelMotor>();if(motor==null)continue;
   var grip=p.FindModuleImplementing<KDAGrip>();
   saved.Add(new Saved{motor=motor,grip=grip,enabled=motor.motorEnabled,auto=motor.autoTorque,traction=motor.tractionControlScale,limiter=motor.driveLimiter});
   bool front=IsFront(p);motor.motorEnabled=!front;
   if(grip!=null)grip.BurnoutScale=front?1.25f:.35f;
   if(!front){motor.autoTorque=false;motor.tractionControlScale=0;motor.driveLimiter=100;}
  }
 }
 public bool IsFront(Part wheel){return part.transform.InverseTransformPoint(wheel.transform.position).y>0;}
 public int PreviousGear {get{return previousGear;}}
 public bool OriginalMotor(KDAWheelMotor motor,out bool enabled,out bool auto,out float traction,out float limiter){
  foreach(var s in saved)if(s.motor==motor){enabled=s.enabled;auto=s.auto;traction=s.traction;limiter=s.limiter;return true;}
  enabled=auto=false;traction=limiter=0;return false;
 }
 void Restore(){
  bool had=saved.Count>0;
  foreach(var s in saved){if(s.motor!=null){s.motor.motorEnabled=s.enabled;s.motor.autoTorque=s.auto;s.motor.tractionControlScale=s.traction;s.motor.driveLimiter=s.limiter;}if(s.grip!=null)s.grip.BurnoutScale=1;}
  saved.Clear();if(had&&engine!=null)engine.RestoreBurnoutGear(previousGear);
 }
 public override void OnFixedUpdate(){if(engine!=null)engine.EnsureStep();}
 void OnDisable(){Launching=false;state.Step(false,false,false,false,false,0);Restore();}
 void OnDestroy(){Restore();}
}
