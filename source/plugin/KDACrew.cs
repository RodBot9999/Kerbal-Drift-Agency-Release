using System;
using System.Collections.Generic;
using UnityEngine;

// Separate crew prototype: no dependency on the vehicle physics assembly.
public sealed class KDADoorway:PartModule {
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Seat / door channel")]
 [UI_FloatRange(minValue=1,maxValue=8,stepIncrement=1)] public float channel=1;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Exit stand-off (m)")]
 [UI_FloatRange(minValue=.2f,maxValue=1.5f,stepIncrement=.05f)] public float standOff=.55f;
 [KSPField(guiActive=true,guiName="Doorway")] public string status="Place arrow pointing outside";
 public int Channel {get{return Mathf.Clamp(Mathf.RoundToInt(channel),1,8);}}
 public Vector3 ExitPoint {get{return part.transform.position+part.transform.forward*standOff;}}
 public Quaternion ExitRotation {get{return Quaternion.LookRotation(-part.transform.forward,part.transform.up);}}
 // A surface-attached marker may inherit an inward normal. Its placement
 // relative to the seat determines which side is outside without rotating art.
 public Vector3 OutwardDirection(Part seat){var direction=part.transform.forward;if(seat!=null&&Vector3.Dot(direction,part.transform.position-seat.transform.position)<-.02f)direction=-direction;return direction;}
 public override void OnStart(StartState s){base.OnStart(s);if(HighLogic.LoadedSceneIsFlight){foreach(var r in part.GetComponentsInChildren<Renderer>())r.enabled=false;foreach(var c in part.GetComponentsInChildren<Collider>())c.enabled=false;status="Linked seat exits here";}}
}

public sealed class KDARaceSeat:PartModule {
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Seat / door channel")]
 [UI_FloatRange(minValue=1,maxValue=8,stepIncrement=1)] public float channel=1;
 [KSPField(guiActive=true,guiActiveEditor=true,guiName="Race seat")] public string status="Place inside car; add doorway";
 Transform hatch;Collider hatchCollider;KDADoorway doorway,lastDoor;float nextRefresh;Vector3 resolvedDirection;float resolvedStandOff,clearanceExtra,lastStandOff;
 bool originalNoAuto;
 KDACrewVisual visual;
 public int Channel {get{return Mathf.Clamp(Mathf.RoundToInt(channel),1,8);}}
 public KDADoorway Door {get{return doorway;}}
 public bool HatchReady {get;private set;}
 public override void OnStart(StartState s){
  base.OnStart(s);originalNoAuto=part.noAutoEVAAny;
  hatch=part.FindModelTransform("KDAAirlock");hatchCollider=hatch==null?null:hatch.GetComponent<Collider>();
  if(HighLogic.LoadedSceneIsFlight){part.noAutoEVAAny=false;visual=gameObject.AddComponent<KDACrewVisual>();visual.Seat=this;GameEvents.onAttemptEva.Add(CheckExit);RefreshDoor();}
 }
 void Update(){
  if(HighLogic.LoadedSceneIsEditor){status="1 real IVA crew slot / channel "+Channel;return;}
  if(!HighLogic.LoadedSceneIsFlight||vessel==null||part.packed)return;
  if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.2f;RefreshDoor();}
 }
 void PlaceHatch(){hatch.position=doorway.part.transform.position+resolvedDirection*resolvedStandOff;hatch.rotation=Quaternion.LookRotation(-resolvedDirection,doorway.part.transform.up);}
 void LateUpdate(){if(HighLogic.LoadedSceneIsFlight&&doorway!=null&&hatch!=null){resolvedDirection=doorway.OutwardDirection(part);PlaceHatch();}}
 void CheckExit(ProtoCrewMember crew,Part from,Transform exit){if(from!=part)return;RefreshDoor();if(!HatchReady){FlightEVA.fetch.overrideEVA=true;ScreenMessages.PostScreenMessage(status,4,ScreenMessageStyle.UPPER_CENTER);}}
 public void RefreshDoor(){
  doorway=null;HatchReady=false;part.noAutoEVAAny=false;if(hatch==null||vessel==null)return;
  float distance=float.PositiveInfinity;
  foreach(Part p in vessel.parts){var d=p.FindModuleImplementing<KDADoorway>();if(d==null||d.Channel!=Channel)continue;float delta=(p.transform.position-part.transform.position).sqrMagnitude;if(delta<distance){distance=delta;doorway=d;}}
  if(doorway==null){if(hatchCollider!=null)hatchCollider.enabled=false;status="Add matching doorway marker";return;}
  bool changed=lastDoor!=doorway||lastStandOff!=doorway.standOff;if(changed)clearanceExtra=0;lastDoor=doorway;lastStandOff=doorway.standOff;
  hatch.gameObject.SetActive(true);resolvedDirection=doorway.OutwardDirection(part);resolvedStandOff=doorway.standOff+clearanceExtra;PlaceHatch();part.airlock=hatch;
  // The native EVA obstruction checks remain authoritative. A blocked
  // marker is never handled by teleporting a Kerbal through the vehicle.
  HatchReady=!FlightEVA.HatchIsObstructedMore(part,hatch);
  // Clearance is relative to this car, rather than a fixed human-size door.
  // Each candidate still passes native terrain/part obstruction checks.
  // Keep an empty seat's entry trigger stationary as the EVA Kerbal approaches.
  if(changed||part.protoModuleCrew.Count>0){resolvedStandOff=doorway.standOff;PlaceHatch();HatchReady=!FlightEVA.HatchIsObstructedMore(part,hatch);
   for(int i=1;!HatchReady&&i<=6;i++){resolvedStandOff=doorway.standOff+i*.15f;PlaceHatch();HatchReady=!FlightEVA.HatchIsObstructedMore(part,hatch);}
   clearanceExtra=resolvedStandOff-doorway.standOff;
  }
  KDARaceSeat selected=null;float nearest=float.PositiveInfinity;
  foreach(Part p in vessel.parts){var seat=p.FindModuleImplementing<KDARaceSeat>();if(seat==null||seat.Channel!=Channel||p.protoModuleCrew.Count>=p.CrewCapacity)continue;
   float delta=(p.transform.position-doorway.part.transform.position).sqrMagnitude;if(delta<nearest){nearest=delta;selected=seat;}}
  // A nearby EVA Kerbal can obstruct the *exit* sphere. Boarding an empty
  // seat must still work, so the entry trigger is independent of that check.
  if(hatchCollider!=null)hatchCollider.enabled=selected==this;
  status=HatchReady?"Door linked / stock B boarding + portrait EVA":"Door exit obstructed; move marker outward";
 }
 [KSPEvent(guiActive=true,guiName="Enter driver view")]
 public void DriverView(){if(part.protoModuleCrew.Count==0)return;if(part.internalModel==null)part.SpawnIVA();if(part.internalModel!=null&&part.internalModel.seats.Count>0){var crew=part.internalModel.seats[0].kerbalRef;if(crew!=null)CameraManager.Instance.SetCameraIVA(crew,true);}}
 [KSPEvent(guiActive=true,guiName="Exit through doorway")]
 public void ExitDoor(){RefreshDoor();if(!HatchReady){ScreenMessages.PostScreenMessage(status,4,ScreenMessageStyle.UPPER_CENTER);return;}if(part.internalModel==null)part.SpawnIVA();if(part.internalModel!=null&&part.internalModel.seats.Count>0){var crew=part.internalModel.seats[0].kerbalRef;if(crew!=null)FlightEVA.SpawnEVA(crew);}}
 void OnDestroy(){GameEvents.onAttemptEva.Remove(CheckExit);if(part!=null)part.noAutoEVAAny=originalNoAuto;}
}

// Config patch removes the old body command pod. This guard also prevents
// remote command modules or lingering throttle trim driving an empty car.
public sealed class KDAImportedCrew:PartModule {
 Vessel subscribed;bool migrated;
 public bool HasDriver {get{if(vessel==null)return false;foreach(Part p in vessel.parts)if(p!=part&&p.CrewCapacity>0&&p.protoModuleCrew.Count>0)return true;return false;}}
 public override void OnStart(StartState s){base.OnStart(s);if(HighLogic.LoadedSceneIsFlight){Subscribe();var shell=gameObject.AddComponent<KDAInteriorShell>();shell.Body=part;}}
 void Subscribe(){if(vessel==subscribed)return;if(subscribed!=null)subscribed.OnFlyByWire-=Guard;subscribed=vessel;if(subscribed!=null)subscribed.OnFlyByWire+=Guard;}
 static void ClearDrive(FlightCtrlState state){state.wheelThrottle=state.wheelThrottleTrim=state.wheelSteer=state.wheelSteerTrim=0;}
 void Guard(FlightCtrlState state){if(!HasDriver)ClearDrive(state);}
 public override void OnFixedUpdate(){if(!HighLogic.LoadedSceneIsFlight||vessel==null||vessel.packed)return;Subscribe();if(!migrated){MigrateLegacyCrew();migrated=true;}if(!HasDriver)ClearDrive(vessel.ctrlState);}
 public void MigrateLegacyCrew(){
  // Old saves may contain assigned Kerbals in the now removed invisible
  // slots. Move them to real KDA seats; return any overflow safely to roster.
  var oldCrew=new List<ProtoCrewMember>(part.protoModuleCrew);
  foreach(var crew in oldCrew){Part target=null;foreach(Part p in vessel.parts)if(p.FindModuleImplementing<KDARaceSeat>()!=null&&p.protoModuleCrew.Count<p.CrewCapacity){target=p;break;}
   part.RemoveCrewmember(crew);
   if(target!=null&&target.AddCrewmember(crew)){target.SpawnIVA();Debug.Log("[KDACREW] Legacy body crew moved to race seat: "+crew.name);}
   else {crew.rosterStatus=ProtoCrewMember.RosterStatus.Available;crew.seat=null;crew.seatIdx=-1;Debug.Log("[KDACREW] Legacy body crew returned to roster: "+crew.name);ScreenMessages.PostScreenMessage(crew.name+" returned to roster; add and crew a KDA Race Seat.",6,ScreenMessageStyle.UPPER_CENTER);}
  }
  part.CrewCapacity=0;
 }
 void OnDestroy(){if(subscribed!=null)subscribed.OnFlyByWire-=Guard;}
}

// Render-only external avatar. Reuse IVA meshes/materials/bone poses, with
// no duplicate Kerbal components, colliders, animation systems or cameras.
public sealed class KDACrewVisual:MonoBehaviour {
 public KDARaceSeat Seat;
 Kerbal source;GameObject root;readonly Dictionary<Transform,Transform> poses=new Dictionary<Transform,Transform>();
 readonly List<Renderer> renderers=new List<Renderer>();
 KDAPortraitRender portrait;
 public int RendererCount {get{return renderers.Count;}}
 void Clear(){if(portrait!=null)Destroy(portrait);portrait=null;if(root!=null)Destroy(root);root=null;source=null;poses.Clear();renderers.Clear();}
 void Build(Kerbal kerbal){
  Clear();source=kerbal;root=new GameObject("KDA seated crew visual");root.transform.SetParent(Seat.part.transform,false);
  foreach(var t in source.GetComponentsInChildren<Transform>(true)){
   var copy=t==source.transform?root.transform:new GameObject(t.name).transform;
   if(t!=source.transform)copy.SetParent(poses[t.parent],false);copy.gameObject.layer=0;poses[t]=copy;
  }
  foreach(var r in source.GetComponentsInChildren<Renderer>(true)){
   Transform target;if(!poses.TryGetValue(r.transform,out target))continue;
   Renderer copy=null;var skin=r as SkinnedMeshRenderer;
   if(skin!=null){var sk=target.gameObject.AddComponent<SkinnedMeshRenderer>();sk.sharedMesh=skin.sharedMesh;sk.localBounds=skin.localBounds;sk.updateWhenOffscreen=true;
    var bones=new Transform[skin.bones.Length];for(int i=0;i<bones.Length;i++)if(skin.bones[i]!=null)poses.TryGetValue(skin.bones[i],out bones[i]);sk.bones=bones;
    Transform bone;if(skin.rootBone!=null&&poses.TryGetValue(skin.rootBone,out bone))sk.rootBone=bone;copy=sk;
   }else {var mesh=r.GetComponent<MeshFilter>();if(mesh!=null){target.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;copy=target.gameObject.AddComponent<MeshRenderer>();}}
   if(copy!=null){copy.sharedMaterials=r.sharedMaterials;renderers.Add(copy);}
  }
  if(source.kerbalCam!=null){portrait=source.kerbalCam.gameObject.AddComponent<KDAPortraitRender>();portrait.Crew=source;}
 }
 void LateUpdate(){
  if(Seat==null||Seat.part==null||Seat.part.internalModel==null){Clear();return;}
  var seats=Seat.part.internalModel.seats;Kerbal occupant=seats.Count==0?null:seats[0].kerbalRef;
  if(occupant==null){Clear();return;}if(source!=occupant)Build(occupant);
  bool external=CameraManager.Instance==null||CameraManager.Instance.currentCameraMode==CameraManager.CameraMode.Flight;
  root.SetActive(external);if(!external)return;
  source.SetVisibleInPortrait(Seat.vessel==FlightGlobals.ActiveVessel);
  root.transform.position=InternalSpace.InternalToWorld(source.transform.position);root.transform.rotation=InternalSpace.InternalToWorld(source.transform.rotation);root.transform.localScale=source.transform.localScale;
  foreach(var pair in poses)if(pair.Key!=null&&pair.Value!=null&&pair.Key!=source.transform){pair.Value.localPosition=pair.Key.localPosition;pair.Value.localRotation=pair.Key.localRotation;pair.Value.localScale=pair.Key.localScale;}
  // IVA camera helpers temporarily hide the head. External visual always
  // shows it, but respects the seat's no-helmet choice and actual accessories.
  foreach(var r in renderers)if(r!=null)r.enabled=!IsHelmet(r.transform,root.transform)||source.showHelmet;
 }
 public static bool IsHelmet(Transform child,Transform stop){for(var t=child;t!=null&&t!=stop;t=t.parent)if(t.name.ToLowerInvariant().Contains("helmet"))return true;return false;}
 void OnDestroy(){Clear();}
}

// Portrait rendering occurs independently of LateUpdate. Enable only its
// crew meshes for that camera's render, then restore IVA visibility exactly.
public sealed class KDAPortraitRender:MonoBehaviour {
 public Kerbal Crew;Renderer[] meshes;bool[] enabledBefore;Light fill;
 void OnPreCull(){if(Crew==null)return;if(fill==null){fill=gameObject.AddComponent<Light>();fill.type=LightType.Point;fill.color=new Color(1,.95f,.9f);fill.intensity=1.2f;fill.range=2;fill.cullingMask=1<<16;fill.shadows=LightShadows.None;}fill.enabled=true;if(meshes==null){meshes=Crew.GetComponentsInChildren<Renderer>(true);enabledBefore=new bool[meshes.Length];}for(int i=0;i<meshes.Length;i++)if(meshes[i]!=null){enabledBefore[i]=meshes[i].enabled;meshes[i].enabled=!KDACrewVisual.IsHelmet(meshes[i].transform,Crew.transform)||Crew.showHelmet;}}
 void OnPostRender(){if(fill!=null)fill.enabled=false;if(meshes==null)return;for(int i=0;i<meshes.Length;i++)if(meshes[i]!=null)meshes[i].enabled=enabledBefore[i];}
 void OnDestroy(){if(fill!=null)Destroy(fill);}
}
