using System;using UnityEngine;using KerbalDriftAgency;
public partial class KDAEngine {
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Nitrous kit")]
 [UI_ChooseOption(options=new[]{"Off","Installed"})] public int nitrousKit=0;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Nitrous shot",guiUnits=" hp")]
 [UI_FloatRange(minValue=25,maxValue=300,stepIncrement=25)] public float nitrousShotHp=100;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Nitrous bottle")]
 [UI_ChooseOption(options=new[]{"Small","Medium","Large"})] public int nitrousBottle=1;
 [KSPField(isPersistant=true)] public int nitrousKey=(int)KeyCode.N;
 [KSPField(isPersistant=true)] public float nitrousCharge=1;
 public bool NitrousActive {get;private set;}public bool NitrousDriftRecharge {get;private set;}
 readonly NitrousCore nitrous=new NitrousCore();KDANitrousSound nitrousSound;
 [KSPAction("Nitrous (hold key)")] public void HoldNitrous(KSPActionParam p){}
 bool NitrousHeld(){
  if(GUIUtility.keyboardControl!=0||InputLockManager.IsLocked(ControlTypes.WHEEL_THROTTLE))return false;
  if(nitrousKey>0&&nitrousKey<(int)KeyCode.Mouse0&&Enum.IsDefined(typeof(KeyCode),nitrousKey)&&Input.GetKey((KeyCode)nitrousKey))return true;
  var action=Actions["HoldNitrous"];if(action==null)return false;
  KeyBinding[] keys={GameSettings.CustomActionGroup1,GameSettings.CustomActionGroup2,GameSettings.CustomActionGroup3,GameSettings.CustomActionGroup4,GameSettings.CustomActionGroup5,GameSettings.CustomActionGroup6,GameSettings.CustomActionGroup7,GameSettings.CustomActionGroup8,GameSettings.CustomActionGroup9,GameSettings.CustomActionGroup10};
  KSPActionGroup[] groups={KSPActionGroup.Custom01,KSPActionGroup.Custom02,KSPActionGroup.Custom03,KSPActionGroup.Custom04,KSPActionGroup.Custom05,KSPActionGroup.Custom06,KSPActionGroup.Custom07,KSPActionGroup.Custom08,KSPActionGroup.Custom09,KSPActionGroup.Custom10};
  for(int i=0;i<keys.Length;i++)if((action.actionGroup&groups[i])!=0&&keys[i].GetKey(false))return true;return false;
 }
 void StepNitrous(double dt){
  NitrousActive=NitrousDriftRecharge=false;
  if(nitrousKit!=1||vessel!=FlightGlobals.ActiveVessel||vessel.packed||FlightDriver.Pause||Time.timeScale<=0||TimeWarp.CurrentRate>1||!Application.isFocused||MapView.MapIsEnabled)return;
  bool grounded=false;int contact=0;foreach(var m in motors)if(m.DriveGrounded)contact++;grounded=contact>=2;
  Vector3 local=part.transform.InverseTransformDirection(vessel.srf_velocity);double angle=Math.Abs(Math.Atan2(local.x,Math.Abs(local.y))*180/Math.PI);
  bool drift=grounded&&vessel.srfSpeed>15/3.6&&angle>=12&&angle<=70;
  nitrous.Charge=nitrousCharge;
  double torque=nitrous.Step(dt,nitrousKit==1,nitrousBottle,nitrousShotHp,NitrousHeld(),engineRunning&&drivenCount>0&&driveSelector>0&&RequestedThrottle>.15f&&!core.Shifting&&!core.LimiterActive&&core.AvailableTorqueNm>0&&core.Rpm>=1500,drift,core.Rpm);
  nitrousCharge=(float)nitrous.Charge;NitrousActive=nitrous.Active;NitrousDriftRecharge=nitrous.DriftRecharge;
  core.AvailableTorqueNm+=torque;core.OutputTorqueNm+=torque*core.Throttle;
  if(nitrousKit==1&&nitrousSound==null){nitrousSound=part.gameObject.AddComponent<KDANitrousSound>();nitrousSound.Engine=this;}
 }
}
public sealed class KDANitrousSound:MonoBehaviour {
 public KDAEngine Engine;AudioSource spray;AudioClip clip;
 void Start(){
  const int rate=22050;float[] data=new float[rate];var random=new System.Random(93614);double low=0;
  for(int i=0;i<data.Length;i++){double n=random.NextDouble()*2-1;low+=.08*(n-low);data[i]=(float)((n-low)*.38);}
  clip=AudioClip.Create("KDA nitrous spray",rate,1,rate,false);clip.SetData(data,0);spray=gameObject.AddComponent<AudioSource>();spray.clip=clip;spray.loop=true;spray.playOnAwake=false;KDAAudioMix.Configure(spray,false);spray.dopplerLevel=.1f;
 }
 void Update(){if(spray==null)return;bool active=Engine!=null&&Engine.vessel!=null&&Engine.NitrousActive&&Engine.vessel==FlightGlobals.ActiveVessel&&!Engine.vessel.packed&&!FlightDriver.Pause&&Time.timeScale>0&&TimeWarp.CurrentRate<=1&&Application.isFocused&&!MapView.MapIsEnabled;
  spray.volume=Mathf.Clamp01(GameSettings.SHIP_VOLUME)*.6f;if(active){if(!spray.isPlaying)spray.Play();}else if(spray.isPlaying)spray.Stop();
 }
 void OnDestroy(){if(spray!=null){spray.Stop();Destroy(spray);}if(clip!=null)Destroy(clip);}
}
