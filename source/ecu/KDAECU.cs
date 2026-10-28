// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using UnityEngine;
using KerbalDriftAgency.ECU;

// Rover assistance only. Native propulsion, tire and suspension settings are never owned.
public sealed partial class KDAECU : PartModule {
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="ECU / SC enabled")] public bool ecuEnabled=true;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Adaptive steering")] public bool adaptiveSteering=true;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Yaw stability / brake aid")] public bool yawStability=true;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="ABS")] public bool absEnabled=true;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Brake traction aid")] public bool brakeTraction=true;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Assistance",guiFormat="P0")]
 [UI_FloatRange(minValue=0,maxValue=1,stepIncrement=.05f)] public float assistance=1;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Cornering allowance",guiUnits="%",guiFormat="F0")]
 [UI_FloatRange(minValue=60,maxValue=160,stepIncrement=5)] public float corneringPercent=100;
 [KSPField(guiActive=true,guiName="Effective cornering budget",guiUnits=" g",guiFormat="F2")] public float effectiveBudget;
 [KSPField(guiActive=true,guiName="ECU status")] public string ecuStatus="Mount arrow forward; open ECU setup";
 internal readonly List<EcuWheel> Wheels=new List<EcuWheel>();
 readonly Dictionary<uint,WheelChoice> choices=new Dictionary<uint,WheelChoice>();
 Vessel subscribed;float nextScan,midZ,wheelbase=2,track=1,cgHeight=.5f;
 internal bool Active;internal float YawError;int leftSteering=-1,rightSteering=-1;
 public sealed class WheelChoice {public int Group;}
 internal bool IsFront(EcuWheel w){var c=Choice(w.Part);return c.Group==1||c.Group==0&&Local(w.Part).z>=midZ;}
 internal WheelChoice Choice(Part p){WheelChoice c;if(!choices.TryGetValue(p.craftID,out c)){c=new WheelChoice();choices[p.craftID]=c;}return c;}
 internal Vector3 Local(Part p){return part.transform.InverseTransformPoint(p.transform.position);}
 public override void OnLoad(ConfigNode node){base.OnLoad(node);addonProfile=AddonProfile.Normalize(addonProfile);choices.Clear();foreach(var n in node.GetNodes("ECU_WHEEL")){uint id;int group;if(uint.TryParse(n.GetValue("craftId"),out id)&&int.TryParse(n.GetValue("group"),out group))choices[id]=new WheelChoice{Group=Mathf.Clamp(group,0,3)};}}
 public override void OnSave(ConfigNode node){base.OnSave(node);node.RemoveNodes("ECU_WHEEL");foreach(var pair in choices){var n=node.AddNode("ECU_WHEEL");n.AddValue("craftId",pair.Key);n.AddValue("group",pair.Value.Group);}}
 public override void OnStart(StartState state){base.OnStart(state);Subscribe();AddonPresentation();}
 void Subscribe(){if(!HighLogic.LoadedSceneIsFlight||vessel==subscribed)return;Release();if(subscribed!=null)subscribed.OnFlyByWire-=Drive;subscribed=vessel;if(subscribed!=null)subscribed.OnFlyByWire+=Drive;nextScan=0;}
 [KSPAction("Toggle ECU / stability control")] public void ToggleAction(KSPActionParam p){ToggleEcu();}
 [KSPEvent(guiActive=true,guiActiveEditor=true,guiName="Toggle ECU / SC")] public void ToggleEcu(){ecuEnabled=!ecuEnabled;if(!ecuEnabled)Release();nextScan=0;}
 [KSPEvent(guiActive=true,guiActiveEditor=true,guiName="ECU setup / wheel groups")] public void OpenSetup(){windowOpen=!windowOpen;if(!windowOpen)UnlockSetup();}
 void Update(){
  Subscribe();AddonPresentation();if(!HighLogic.LoadedSceneIsFlight)return;
  if(!ecuEnabled||!EcuHooks.Ready||vessel==null||vessel.packed||vessel!=FlightGlobals.ActiveVessel)Release();
  if(Time.realtimeSinceStartup>=nextScan){nextScan=Time.realtimeSinceStartup+.5f;Scan();}
 }
 void Scan(){
  if(!ecuEnabled||!EcuHooks.Ready||vessel==null||vessel.packed||vessel!=FlightGlobals.ActiveVessel){Release();ecuStatus=!EcuHooks.Ready?"ECU hook unavailable":!ecuEnabled?"OFF - native rover restored":"Standby";return;}
  // Never touch existing KDA cars; one deterministic ECU owner per vessel.
  KDAECU owner=null;foreach(var p in vessel.parts){if(p.FindModuleImplementing<KDAEngine>()!=null||p.FindModuleImplementing<KDAController>()!=null){Release();ecuStatus="Existing KDA car - ECU bypassed";return;}foreach(PartModule m in p.Modules){var ecu=m as KDAECU;if(ecu!=null&&ecu.ecuEnabled&&(owner==null||ecu.part.flightID<owner.part.flightID))owner=ecu;}}
  if(owner!=this){Release();ecuStatus="Standby - another ECU owns this rover";return;}
  var bases=new List<ModuleWheelBase>();foreach(var p in vessel.parts)foreach(PartModule m in p.Modules){var b=m as ModuleWheelBase;if(b!=null&&b.GetType()==typeof(ModuleWheelBase)&&b.Wheel!=null)bases.Add(b);}
  bool changed=bases.Count!=Wheels.Count;foreach(var w in Wheels)if(w.Part==null||w.Part.vessel!=vessel||!bases.Contains(w.Base)||w.Base.Wheel!=w.Wheel)changed=true;
  if(changed){Release();foreach(var b in bases)Wheels.Add(new EcuWheel(this,b));}
  if(Wheels.Count<4){Release();ecuStatus="Requires at least four stock rover wheels";return;}
  float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue;var points=new WheelPoint[Wheels.Count];
  for(int i=0;i<Wheels.Count;i++){var w=Wheels[i];Vector3 pos=Local(w.Part);var c=Choice(w.Part);points[i]=new WheelPoint{X=pos.x,Z=pos.z,CanSteer=w.Steering!=null,Group=c.Group};minX=Mathf.Min(minX,pos.x);maxX=Mathf.Max(maxX,pos.x);minZ=Mathf.Min(minZ,pos.z);maxZ=Mathf.Max(maxZ,pos.z);}
  var pair=EcuCore.FrontPair(points);leftSteering=pair[0];rightSteering=pair[1];
  if(leftSteering<0||rightSteering<0){Release();ecuStatus="Choose one front steering wheel on each side";return;}
  track=Mathf.Max(.2f,maxX-minX);wheelbase=Mathf.Max(.2f,maxZ-minZ);midZ=(maxZ+minZ)*.5f;
  float meanY=0;foreach(var w in Wheels)meanY+=Local(w.Part).y-w.Wheel.WheelRadius;
  cgHeight=Mathf.Max(.15f,part.transform.InverseTransformPoint((Vector3)vessel.CoM).y-meanY/Wheels.Count);
  Active=true;for(int i=0;i<Wheels.Count;i++)Wheels[i].Activate(i==leftSteering||i==rightSteering);
  ecuStatus="ECU: 2 steering / native power";
 }
 void Drive(FlightCtrlState state){if(!Active||!ecuEnabled||vessel==null||vessel.packed||vessel!=FlightGlobals.ActiveVessel)return;DriveAddon(state);}
 internal void Release(){Active=false;foreach(var w in Wheels)w.Restore();Wheels.Clear();YawError=0;ResetAddonState();}
 void OnDestroy(){Release();if(subscribed!=null)subscribed.OnFlyByWire-=Drive;UnlockSetup();}
 public override string GetInfo(){return "Rover ECU: Unstable and Normal stability profiles, adaptive steering, yaw braking, ABS and brake traction. Native motors, tires and suspension are retained. Mount arrow forward/up. Exactly two front wheels steer while ON; OFF restores their original steering setup.";}
}
