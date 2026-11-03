using ModuleWheels;
using UnityEngine;

public class KDACarControls : PartModule {
 public bool HandbrakeActive {get;private set;}
 // The action is a binding marker; group toggle events must not latch it.
 [KSPAction("Rear handbrake (hold key)")] public void HoldHandbrake(KSPActionParam p){}
 static readonly KSPActionGroup[] handbrakeGroups={KSPActionGroup.Custom01,KSPActionGroup.Custom02,KSPActionGroup.Custom03,KSPActionGroup.Custom04,KSPActionGroup.Custom05,KSPActionGroup.Custom06,KSPActionGroup.Custom07,KSPActionGroup.Custom08,KSPActionGroup.Custom09,KSPActionGroup.Custom10};
 void ReadHandbrake(){
  HandbrakeActive=false;
  if(!HighLogic.LoadedSceneIsFlight||vessel==null||vessel!=FlightGlobals.ActiveVessel||vessel.packed||FlightDriver.Pause||Time.timeScale<=0||!Application.isFocused||MapView.MapIsEnabled||InputLockManager.IsLocked(ControlTypes.WHEEL_THROTTLE))return;
  var action=Actions["HoldHandbrake"];if(action==null)return;
  KeyBinding[] keys={GameSettings.CustomActionGroup1,GameSettings.CustomActionGroup2,GameSettings.CustomActionGroup3,GameSettings.CustomActionGroup4,GameSettings.CustomActionGroup5,GameSettings.CustomActionGroup6,GameSettings.CustomActionGroup7,GameSettings.CustomActionGroup8,GameSettings.CustomActionGroup9,GameSettings.CustomActionGroup10};
  for(int i=0;i<handbrakeGroups.Length;i++)if((action.actionGroup&handbrakeGroups[i])!=0&&keys[i].GetKey(false)){HandbrakeActive=true;break;}
 }
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Car TCS")] public bool tcsEnabled=true;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Car ABS")] public bool absEnabled=true;
 [KSPEvent(guiActive=true,guiActiveEditor=true,guiName="ABS ENABLE")] public void EnableAbs(){absEnabled=true;}
 [KSPEvent(guiActive=true,guiActiveEditor=true,guiName="ABS DISABLE")] public void DisableAbs(){absEnabled=false;}
 [KSPAction("Toggle ABS")] public void ToggleAbs(KSPActionParam p){absEnabled=!absEnabled;}
 [KSPAction("Disable all driver aids")] public void DisableAllAction(KSPActionParam p){DisableAll();}
 [KSPEvent(guiActive=true,guiActiveEditor=true,guiName="Disable all driver aids")] public void DisableAll(){tcsEnabled=absEnabled=false;var sc=part.FindModuleImplementing<KDAController>();if(sc!=null)sc.stabilityEnabled=false;Apply();}
 [KSPEvent(guiActive=true,guiActiveEditor=true,guiName="Enable all driver aids")] public void EnableAll(){tcsEnabled=absEnabled=true;var sc=part.FindModuleImplementing<KDAController>();if(sc!=null)sc.stabilityEnabled=true;Apply();}
 [KSPEvent(guiActive=true,guiActiveEditor=true,guiName="TCS ENABLE",active=true)] public void EnableTcs(){tcsEnabled=true;Apply();}
 [KSPEvent(guiActive=true,guiActiveEditor=true,guiName="TCS DISABLE",active=true)] public void DisableTcs(){tcsEnabled=false;Apply();}
 [KSPAction("TCS enable")] public void EnableAction(KSPActionParam p){EnableTcs();}
 [KSPAction("TCS disable")] public void DisableAction(KSPActionParam p){DisableTcs();}
 [KSPAction("Toggle TCS")] public void ToggleAction(KSPActionParam p){tcsEnabled=!tcsEnabled;Apply();}
 public bool DriftLocked {get{var sc=part.FindModuleImplementing<KDAController>();return sc!=null&&sc.driveMode==3;}}
 public bool TcsActive {get{return tcsEnabled&&!DriftLocked;}}
 KDAEngine engine;
 bool previous;
 public override void OnStart(StartState s){base.OnStart(s);HandbrakeActive=false;engine=part.FindModuleImplementing<KDAEngine>();Apply();}
 public bool Front(Part p){return part.transform.InverseTransformPoint(p.transform.position).y>0;}
 public void Apply(){
  if(DriftLocked)tcsEnabled=false;
  if(engine==null)engine=part.FindModuleImplementing<KDAEngine>();
  if(HighLogic.LoadedSceneIsEditor){if(EditorLogic.fetch!=null)foreach(Part p in EditorLogic.fetch.ship.parts)Set(p,false);}
  else if(vessel!=null)foreach(Part p in vessel.parts)Set(p,engine!=null&&(engine.BurnoutActive||engine.BurnoutLaunching));
 }
 void Set(Part p,bool transient){
  var motor=p.FindModuleImplementing<KDAWheelMotor>();if(motor==null)return;
  if(transient){motor.autoTorque=false;motor.tractionControlScale=0;motor.driveLimiter=100;return;}
  // The custom rate-limited TCS owns torque. Stock autoTorque would apply
  // a second, uncontrolled torque cut and defeat its smooth response.
  motor.autoTorque=false;motor.tractionControlScale=0;motor.driveLimiter=100;
 }
 public override void OnFixedUpdate(){ReadHandbrake();Apply();}
 void Update(){ReadHandbrake();if(HighLogic.LoadedSceneIsEditor&&previous!=tcsEnabled){previous=tcsEnabled;Apply();}}
}
