using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Optional Kerbal Konstructs bridge. Reflection keeps ordinary KDA installs independent of KK.
[KSPAddon(KSPAddon.Startup.MainMenu,true)]
public sealed class KDAWorldGarage:MonoBehaviour {
 public static KDAWorldGarage Instance {get;private set;}
 public static bool ActiveSession {get{return Instance!=null&&Instance.editorSession;}}
 const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 public sealed class Location {public object Native;public string Id;public Transform Mesh;public CelestialBody Body;internal Transform Entrance;internal bool EntranceResolved;public string SiteName {get{return "KDA Garage "+Id;}}}
 readonly List<Location> garages=new List<Location>();
 Location selected,nearby;bool editorSession,opening,launching,listOpen,startEmpty;float scanAt;string notification="",saveFolder="",incomingId="";
 string message {get{return notification;}set{notification=value;if(editorSession&&KDAGarage.Instance!=null&&KDAGarage.Instance.IsOpen)KDAGarage.Instance.ReportWorldStatus(value);}}
 readonly List<string> incomingCrew=new List<string>();
 readonly HashSet<uint> incomingParts=new HashSet<uint>();
 sealed class HoverSurface {public Renderer Renderer;public MaterialPropertyBlock Original;}
 readonly List<HoverSurface> hoverSurfaces=new List<HoverSurface>();Transform hoverMesh;
 KDAWorkshopRoom room;KeyCode returnKey=KeyCode.F8;
 public IList<Location> Locations {get{return garages.AsReadOnly();}}
 public Location Selected {get{return selected;}}
 public bool IsOpening {get{return opening;}}
 public bool InteriorVisible {get{return room!=null;}}
 public Vector3 WorkshopBayFocus {get{return room==null?new Vector3(0,1.2f,0):room.BayFocus;}}
 void PrepareWorkshop(){if(room==null)room=new KDAWorkshopRoom();room.Update(EditorLogic.fetch.ship);}
 public string Status {get{return message;}}
 public string SessionCraftPath {get{return Path.Combine(SessionDirectory,"Current.craft");}}
 string SessionDirectory {get{return Path.Combine(KSPUtil.ApplicationRootPath,"saves",HighLogic.SaveFolder,"KDA-Garage",selected==null?"unselected":selected.Id);}}
 static object Get(object obj,string name){if(obj==null)return null;Type t=obj as Type??obj.GetType();var f=t.GetField(name,Flags);if(f!=null)return f.GetValue(obj is Type?null:obj);var p=t.GetProperty(name,Flags);return p==null?null:p.GetValue(obj,null);}
 static object Call(object obj,string name,params object[] args){Type t=obj as Type??obj.GetType();foreach(var m in t.GetMethods(Flags))if(m.Name==name&&m.GetParameters().Length==args.Length)return m.Invoke(obj is Type?null:obj,args);throw new MissingMethodException(t.FullName,name);}
 static Type KK(string name){return Type.GetType("KerbalKonstructs.Core."+name+", KerbalKonstructs");}
 void Awake(){if(Instance!=null){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);var config=GameDatabase.Instance.GetConfigNode("KDAGarageBuilding/WorldGarage/KDA_WORLD_GARAGE");if(config!=null){KeyCode key;if(Enum.TryParse<KeyCode>(config.GetValue("returnKey"),true,out key))returnKey=key;}}
 void OnDestroy(){ClearOverviewHover();if(room!=null)room.Dispose();if(Instance==this)Instance=null;}
 static bool GaragePauseOpen(){var menu=Get(typeof(PauseMenu),"fetch") as PauseMenu;return menu!=null&&PauseMenu.isOpen;}
 Location OverviewGarageAtMouse(){
  // Space Center has no PauseMenu instance; its static isOpen getter dereferences fetch.
  if(HighLogic.LoadedScene!=GameScenes.SPACECENTER||opening||GaragePauseOpen()||GUIUtility.hotControl!=0||(ulong)InputLockManager.GetControlLock("KDA.Garage.Launcher")!=0)return null;
  var mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
  if(listOpen&&new Rect(Screen.width-320,141,300,85+garages.Count*38).Contains(mouse))return null;
  if(UnityEngine.EventSystems.EventSystem.current!=null&&UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())return null;
  var camera=Camera.main;if(camera==null)return null;RaycastHit hit;if(!Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition),out hit,20000))return null;
  foreach(var g in garages)if(g.Mesh!=null&&(hit.transform==g.Mesh||hit.transform.IsChildOf(g.Mesh)))return g;return null;
 }
 void ClearOverviewHover(){foreach(var surface in hoverSurfaces)if(surface.Renderer!=null)surface.Renderer.SetPropertyBlock(surface.Original);hoverSurfaces.Clear();hoverMesh=null;}
 void LateUpdate(){
  var hover=OverviewGarageAtMouse();var mesh=hover==null?null:hover.Mesh;if(mesh==hoverMesh)return;ClearOverviewHover();if(mesh==null)return;hoverMesh=mesh;
  foreach(var renderer in mesh.GetComponentsInChildren<Renderer>(false)){
   var material=renderer.sharedMaterial;if(material==null)continue;
   var original=new MaterialPropertyBlock();renderer.GetPropertyBlock(original);hoverSurfaces.Add(new HoverSurface{Renderer=renderer,Original=original});
   var highlight=new MaterialPropertyBlock();renderer.GetPropertyBlock(highlight);
   // Bright green rim and a broad tint make this garage stand out from stock facility hover effects.
   if(material.HasProperty("_Color"))highlight.SetColor("_Color",Color.Lerp(material.GetColor("_Color"),new Color(.25f,1f,.5f,1),.65f));
   if(material.HasProperty("_RimColor")){highlight.SetColor("_RimColor",new Color(.2f,3f,.7f,1));highlight.SetFloat("_RimFalloff",1.2f);}
   renderer.SetPropertyBlock(highlight);
  }
 }
 public void RefreshLocations(){
  garages.Clear();var type=KK("StaticDatabase");if(type==null)return;
  var all=Get(type,"allStaticInstances") as IEnumerable;if(all==null)return;
  foreach(var native in all){var model=Get(native,"model");if((string)Get(model,"name")!="KDAGarage"||(bool)Get(native,"isDestroyed"))continue;
   var mesh=Get(native,"mesh") as GameObject;if(mesh==null)continue;
   Guid id;if(!Guid.TryParse((string)Get(native,"UUID"),out id))continue;
   var location=new Location{Native=native,Id=id.ToString(),Mesh=mesh.transform,Body=Get(native,"CelestialBody") as CelestialBody};
   garages.Add(location);if(selected!=null&&selected.Id==location.Id)selected=location;
  }
 }
 Transform DoorFrame(Location location){if(!location.EntranceResolved){location.EntranceResolved=true;foreach(var t in location.Mesh.GetComponentsInChildren<Transform>(true))if(t.name=="KDAEntrance"){location.Entrance=t;break;}}return location.Entrance!=null?location.Entrance:location.Mesh;}
 public Vector3 DoorPoint(Location location){var frame=DoorFrame(location);return frame==location.Mesh?location.Mesh.TransformPoint(new Vector3(0,0,5.65f)):frame.position;}
 public Vector3 OutsidePoint(Location location){return DoorPoint(location)+DoorFrame(location).forward.normalized*7;}
 public bool CanEnter(Vessel vessel,Location location){
  if(vessel==null||location==null||location.Mesh==null||vessel.packed||vessel.mainBody!=location.Body||!vessel.Landed||vessel.srfSpeed>.75||TimeWarp.CurrentRate>1||GaragePauseOpen())return false;
  bool car=false;foreach(var p in vessel.parts)if(p.FindModuleImplementing<KDAEngine>()!=null){car=true;break;}if(!car)return false;
  var frame=DoorFrame(location);var offset=vessel.transform.position-DoorPoint(location);
  return KerbalDriftAgency.GarageEntranceCore.Contains(Vector3.Dot(offset,frame.right.normalized),Vector3.Dot(offset,frame.forward.normalized),Vector3.Dot(offset,frame.up.normalized),2.65*Math.Abs(frame.lossyScale.x));
 }
 void Update(){
  if(HighLogic.CurrentGame==null){if(room!=null){room.Dispose();room=null;}editorSession=opening=launching=false;return;}
  if(saveFolder!=HighLogic.SaveFolder){saveFolder=HighLogic.SaveFolder;selected=null;editorSession=opening=launching=false;incomingId="";incomingCrew.Clear();if(room!=null){room.Dispose();room=null;}}
  bool supported=HighLogic.LoadedScene==GameScenes.SPACECENTER||HighLogic.LoadedSceneIsFlight||HighLogic.LoadedSceneIsEditor;
  if(!supported){if(room!=null){room.Dispose();room=null;}editorSession=opening=launching=false;return;}
  if(Time.realtimeSinceStartup>=scanAt){scanAt=Time.realtimeSinceStartup+1;try{RefreshLocations();}catch(Exception ex){message="Garage discovery: "+ex.GetBaseException().Message;}}
  if(editorSession&&HighLogic.LoadedSceneIsEditor){if(KDAGarage.Instance!=null&&KDAGarage.Instance.IsOpen&&EditorLogic.fetch!=null)PrepareWorkshop();else if(!opening&&room!=null){room.Dispose();room=null;}return;}
  if(room!=null){room.Dispose();room=null;}
  nearby=null;if(HighLogic.LoadedSceneIsFlight&&!opening&&!launching){var vessel=FlightGlobals.ActiveVessel;float distance=float.MaxValue;foreach(var g in garages)if(CanEnter(vessel,g)){float d=(vessel.transform.position-DoorPoint(g)).sqrMagnitude;if(d<distance){nearby=g;distance=d;}}if(nearby!=null&&Input.GetKeyDown(returnKey)&&GUIUtility.keyboardControl==0)Enter(nearby,vessel);}
  if(Input.GetMouseButtonUp(0)){var clicked=OverviewGarageAtMouse();if(clicked!=null){selected=clicked;listOpen=true;}}
 }
 public bool Enter(Location location,Vessel vessel){
  if(opening||launching||location==null||location.Mesh==null)return false;
  if(vessel!=null&&!CanEnter(vessel,location)){message="Stop the KDA car near the garage entrance first.";return false;}
  selected=location;incomingId="";incomingCrew.Clear();incomingParts.Clear();
  try{
   Directory.CreateDirectory(SessionDirectory);
   if(vessel!=null){
    var recovery=Path.Combine(SessionDirectory,"Recovery");Directory.CreateDirectory(recovery);string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
    var snapshot=new ConfigNode("KDA_GARAGE_VESSEL");vessel.BackupVessel().Save(snapshot.AddNode("VESSEL"));snapshot.Save(Path.Combine(recovery,stamp+".cfg"));
    var construct=new ShipConstruct(vessel.vesselName,EditorFacility.SPH,new List<Part>(vessel.parts));var craft=construct.SaveShip();
    // Flight part transforms are world-space. Normalize into the same upright SPH frame as imported cars.
    var basis=Quaternion.Euler(90,0,0)*Quaternion.Inverse(vessel.rootPart.transform.rotation);var parts=craft.GetNodes("PART");
    if(parts.Length!=vessel.parts.Count)throw new InvalidDataException("The current car could not be serialized completely.");
    for(int i=0;i<parts.Length;i++){parts[i].SetValue("pos",KSPUtil.WriteVector(new Vector3(0,5,0)+basis*(vessel.parts[i].transform.position-vessel.rootPart.transform.position)),true);parts[i].SetValue("rot",KSPUtil.WriteQuaternion(basis*vessel.parts[i].transform.rotation),true);}
    craft.SetValue("type","SPH",true);WriteCraft(craft,SessionCraftPath);craft.Save(Path.Combine(recovery,stamp+".craft"));
    incomingId=vessel.id.ToString();foreach(var p in vessel.parts)incomingParts.Add(p.craftID);foreach(var crew in vessel.GetVesselCrew())incomingCrew.Add(crew.name);
   }
   startEmpty=!File.Exists(SessionCraftPath);editorSession=opening=true;listOpen=false;message="Entering garage...";StartCoroutine(OpenEditor());
   if(!startEmpty)EditorDriver.StartAndLoadVessel(SessionCraftPath,EditorFacility.SPH);else EditorDriver.StartEditor(EditorFacility.SPH);
   return true;
  }catch(Exception ex){editorSession=opening=false;message="Could not enter garage: "+ex.GetBaseException().Message;Debug.LogError("[KDA World Garage] "+ex);return false;}
 }
 IEnumerator OpenEditor(){
  float end=Time.realtimeSinceStartup+90;
  while(Time.realtimeSinceStartup<end&&(!HighLogic.LoadedSceneIsEditor||EditorLogic.fetch==null||EditorLogic.fetch.ship==null||KDAGarage.Instance==null||!IncomingPartsLoaded()))yield return null;
  if(!HighLogic.LoadedSceneIsEditor||KDAGarage.Instance==null||!IncomingPartsLoaded()){editorSession=opening=false;message="Editor did not load the complete car; the original car has been kept.";yield break;}
  yield return null;yield return new WaitForEndOfFrame();
  if(startEmpty){typeof(EditorLogic).GetMethod("OnNewShipConfirm",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(EditorLogic.fetch,null);startEmpty=false;}
  PrepareWorkshop();if(KDAGarage.Instance.Open()){
   // Only remove the flight record after its complete craft has successfully loaded in the editor.
   if(incomingId.Length>0){var saved=HighLogic.CurrentGame.flightState.protoVessels;for(int i=saved.Count-1;i>=0;i--)if(saved[i].vesselID.ToString()==incomingId)saved.RemoveAt(i);
    foreach(var name in incomingCrew){var crew=HighLogic.CurrentGame.CrewRoster[name];if(crew!=null)crew.rosterStatus=ProtoCrewMember.RosterStatus.Available;}
    incomingId="";GamePersistence.SaveGame("persistent",HighLogic.SaveFolder,SaveMode.OVERWRITE);
   }
   opening=false;message="Garage ready. Apply tuning before driving out.";Debug.Log("[KDA World Garage] editor ready");
  }else{editorSession=opening=false;message="Could not open the KDA workbench; the original car has been kept.";}
 }
 bool IncomingPartsLoaded(){if(incomingId.Length==0)return true;if(EditorLogic.fetch==null||EditorLogic.fetch.ship==null||EditorLogic.fetch.ship.parts.Count!=incomingParts.Count)return false;foreach(var p in EditorLogic.fetch.ship.parts)if(!incomingParts.Contains(p.craftID))return false;return true;}
 static void WriteCraft(ConfigNode craft,string path){string temporary=path+".tmp";craft.Save(temporary);var check=ConfigNode.Load(temporary);if(check==null||check.GetNodes("PART").Length!=craft.GetNodes("PART").Length)throw new IOException("Craft verification failed.");if(File.Exists(path))File.Replace(temporary,path,path+".previous");else File.Move(temporary,path);}
 public string EnsureLaunchSite(Location location){
  if(location==null||location.Mesh==null)throw new InvalidOperationException("The placed garage is unavailable.");
  Transform point=location.Mesh.Find("KDALaunchPoint");if(point==null){point=new GameObject("KDALaunchPoint").transform;point.SetParent(location.Mesh,false);}
  var door=DoorFrame(location);Vector3 up=door.up.normalized;Vector3 position=OutsidePoint(location);float height=float.NegativeInfinity;
  foreach(var hit in Physics.RaycastAll(position+up*40,-up,90))if(hit.collider.GetComponentInParent<Part>()==null&&!hit.transform.IsChildOf(location.Mesh)){float h=Vector3.Dot(hit.point-position,up);if(h>height)height=h;}
  if(!float.IsNegativeInfinity(height))position+=up*height;
  point.position=position+up*.1f;point.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(door.forward,up).normalized,up);point.localScale=Vector3.one;
  // KK validates a custom site's named transform against its model prefab as well as the placed instance.
  var prefab=Get(Get(location.Native,"model"),"prefab") as GameObject;if(prefab==null)throw new InvalidOperationException("The garage model is unavailable.");var template=prefab.transform.Find("KDALaunchPoint");if(template==null){template=new GameObject("KDALaunchPoint").transform;template.SetParent(prefab.transform,false);}template.localPosition=point.localPosition;template.localRotation=point.localRotation;
  var manager=KK("LaunchSiteManager");if(manager==null)throw new InvalidOperationException("Kerbal Konstructs is not loaded.");
  var site=Call(manager,"GetLaunchSiteByName",location.SiteName);
  if(site==null){var node=new ConfigNode("LaunchSite");node.AddValue("LaunchSiteName",location.SiteName);node.AddValue("LaunchPadTransform","KDALaunchPoint");node.AddValue("LaunchSiteType","SPH");node.AddValue("LaunchSiteDescription","KDA garage driveway");node.AddValue("Category","Other");node.AddValue("LaunchSiteLength",20);node.AddValue("LaunchSiteWidth",12);node.AddValue("LaunchSiteHeight",12);node.AddValue("OpenCost",0);node.AddValue("CloseValue",0);node.AddValue("InitialCameraRotation",180);node.AddValue("OpenCloseState","Open");Call(manager,"CreateLaunchSite",location.Native,node);site=Call(manager,"GetLaunchSiteByName",location.SiteName);}
  if(site==null)throw new InvalidOperationException("Could not register the garage driveway.");
  Call(manager,"OpenLaunchSite",site);return location.SiteName;
 }
 public bool Launch(){
  if(!editorSession||opening||launching||KDAGarage.Instance==null||!KDAGarage.Instance.IsOpen)return false;var garage=KDAGarage.Instance;var ship=EditorLogic.fetch.ship;
  if(garage.PendingChanges>0||!garage.ValidInputs||garage.HasLoosePart){message="Apply or discard previews, and finish placing parts before launching.";return false;}
  if(garage.Body==null){message="Load a complete KDA car first.";return false;}
  try{
   string site=EnsureLaunchSite(selected);var siteManager=KK("LaunchSiteManager");Call(siteManager,"setLaunchSite",Call(siteManager,"GetLaunchSiteByName",site));var point=selected.Mesh.Find("KDALaunchPoint");
   foreach(var v in FlightGlobals.Vessels)if(v!=null&&v.mainBody==selected.Body&&(v.transform.position-point.position).sqrMagnitude<100){message="Move the vehicle parked in the driveway before launching.";return false;}
   Directory.CreateDirectory(SessionDirectory);var craft=ship.SaveShip();WriteCraft(craft,SessionCraftPath);
   var crew=HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(craft,null,true,true);
   int returning=0;foreach(var seats in crew.GetCrewableParts()){var occupants=seats.GetPartCrew();for(int seat=0;seat<occupants.Length&&returning<incomingCrew.Count;seat++){var driver=HighLogic.CurrentGame.CrewRoster[incomingCrew[returning++]];if(driver!=null&&driver.rosterStatus==ProtoCrewMember.RosterStatus.Available){crew.RemoveCrewMember(driver.name);seats.RemoveCrewFromSeat(seat);seats.AddCrewToSeat(driver,seat);}}}
   if(crew.CrewCount==0){message="A driver is required. Add a race seat and make a Kerbal available.";return false;}
   launching=true;message="Driving out...";garage.Close();editorSession=false;
   FlightDriver.StartWithNewLaunch(SessionCraftPath,"Squad/Flags/default",site,crew);StartCoroutine(WatchLaunch());return true;
  }catch(Exception ex){launching=false;editorSession=true;if(KDAGarage.Instance!=null&&!KDAGarage.Instance.IsOpen)KDAGarage.Instance.Open();message="Launch stopped: "+ex.GetBaseException().Message;Debug.LogError("[KDA World Garage] "+ex);return false;}
 }
 IEnumerator WatchLaunch(){float end=Time.realtimeSinceStartup+90;while(Time.realtimeSinceStartup<end&&!HighLogic.LoadedSceneIsFlight)yield return null;yield return new WaitForSecondsRealtime(4);launching=false;message="";}
 public bool ExitEditor(){
  if(!editorSession||opening||launching)return false;var garage=KDAGarage.Instance;
  if(garage!=null&&garage.PendingChanges>0){message="Apply or discard preview changes before leaving the garage.";return false;}
  try{if(EditorLogic.fetch.ship.parts.Count>0){Directory.CreateDirectory(SessionDirectory);WriteCraft(EditorLogic.fetch.ship.SaveShip(),SessionCraftPath);}else if(File.Exists(SessionCraftPath)){File.Copy(SessionCraftPath,SessionCraftPath+".previous",true);File.Delete(SessionCraftPath);}
   if(garage!=null)garage.Close();editorSession=false;HighLogic.LoadScene(GameScenes.SPACECENTER);return true;
  }catch(Exception ex){message="Could not save the garage session: "+ex.Message;return false;}
 }
 public bool OpenFromToolbar(){
  if(opening||launching)return false;
  try{
   RefreshLocations();
   var location=selected!=null?garages.Find(g=>g.Id==selected.Id):null;if(location==null&&garages.Count>0)location=garages[0];
   if(HighLogic.LoadedSceneIsEditor&&KDAGarage.Instance!=null){
    if(location==null)return KDAGarage.Instance.Open();
    // Enter the building's workshop in-place: do not reload an old session
    // craft over the unsaved car the player is already editing.
    selected=location;editorSession=true;
    try{PrepareWorkshop();if(KDAGarage.Instance.Open()){message="Garage ready. Your editor car has been kept.";return true;}}
    catch{editorSession=false;if(room!=null){room.Dispose();room=null;}throw;}
    editorSession=false;if(room!=null){room.Dispose();room=null;}return false;
   }
   if(location!=null)return Enter(location,null);
   opening=true;StartCoroutine(OpenOverlayFromSpaceCenter());EditorDriver.StartEditor(EditorFacility.SPH);return true;
  }catch(Exception ex){opening=false;message="Could not open garage: "+ex.GetBaseException().Message;Debug.LogError("[KDA World Garage] "+ex);ScreenMessages.PostScreenMessage(message,6,ScreenMessageStyle.UPPER_CENTER);return false;}
 }
 IEnumerator OpenOverlayFromSpaceCenter(){
  float end=Time.realtimeSinceStartup+90;
  while(Time.realtimeSinceStartup<end&&(!HighLogic.LoadedSceneIsEditor||EditorLogic.fetch==null||EditorLogic.fetch.ship==null||KDAGarage.Instance==null))yield return null;
  yield return null;opening=false;
  if(HighLogic.LoadedSceneIsEditor&&KDAGarage.Instance!=null)KDAGarage.Instance.Open();
 }
 public bool ReturnToSPH(){
  if(!editorSession||opening||launching)return false;var garage=KDAGarage.Instance;
  if(garage!=null&&garage.PendingChanges>0){message="Apply or discard preview changes before returning to the SPH.";return false;}
  try{
   if(EditorLogic.fetch.ship.parts.Count>0){Directory.CreateDirectory(SessionDirectory);WriteCraft(EditorLogic.fetch.ship.SaveShip(),SessionCraftPath);}
   if(garage!=null)garage.Close();editorSession=false;
   if(room!=null){room.Dispose();room=null;}message="";
   return true;
  }catch(Exception ex){message="Could not return to SPH: "+ex.Message;return false;}
 }
 GUIStyle hoverTitle,hoverDescription;
 void DrawOverviewTooltip(){
  if(OverviewGarageAtMouse()==null)return;
  var previousSkin=GUI.skin;GUI.skin=HighLogic.Skin;
  if(hoverTitle==null){hoverTitle=new GUIStyle(GUI.skin.label);hoverTitle.fontStyle=FontStyle.Bold;hoverTitle.normal.textColor=Color.yellow;hoverDescription=new GUIStyle(GUI.skin.label);hoverDescription.wordWrap=true;hoverDescription.fontSize=12;}
  float width=Mathf.Min(280,Screen.width-16),height=90;
  float x=Mathf.Clamp(Input.mousePosition.x+18,8,Screen.width-width-8);
  float y=Mathf.Clamp(Screen.height-Input.mousePosition.y+18,8,Screen.height-height-8);
  var rect=new Rect(x,y,width,height);
  GUI.Box(rect,GUIContent.none);
  GUI.Label(new Rect(x+10,y+5,width-20,23),"KDA Garage",hoverTitle);
  GUI.Label(new Rect(x+10,y+29,width-20,height-34),"Build, tune and choose your KDA cars. Click the building to select a garage and enter the workshop.",hoverDescription);
  GUI.skin=previousSkin;
 }
 void OnGUI(){
  if(HighLogic.CurrentGame==null)return;
  if(HighLogic.LoadedScene==GameScenes.SPACECENTER&&!opening&&garages.Count>0){
   DrawOverviewTooltip();
   if(listOpen){GUI.Box(new Rect(Screen.width-320,141,300,85+garages.Count*38),"KDA workshop");for(int i=0;i<garages.Count;i++){var g=garages[i];if(GUI.Button(new Rect(Screen.width-308,174+i*38,276,32),"ENTER GARAGE "+(i+1)))Enter(g,null);}GUI.Label(new Rect(Screen.width-308,180+garages.Count*38,280,35),"Select the building or use this list.");}
  }
  if(nearby!=null&&!opening&&HighLogic.LoadedSceneIsFlight)GUI.Box(new Rect(Screen.width*.5f-210,Screen.height-140,420,52),"KDA GARAGE  /  Press "+returnKey+" to enter and edit\nYour current car and tuning will be kept.");
  if((opening||launching)&&message.Length>0)GUI.Box(new Rect(Screen.width*.5f-310,Screen.height-120,620,28),message);
 }
}
