using System;using System.IO;using System.Collections.Generic;using System.Globalization;using UnityEngine;

public sealed partial class KDAGarage {
 bool prebuildDialog;Vector2 prebuildScroll;
 sealed class PrebuildEntry {public AvailablePart Body;public string Template,Saved;}
 readonly List<PrebuildEntry> prebuildCars=new List<PrebuildEntry>();
 static string CraftPartName(ConfigNode node){string id=node.GetValue("part")??"";int n=id.LastIndexOf('_');return n>0?id.Substring(0,n):id;}
 static string CraftBody(ConfigNode craft){if(craft==null)return "";foreach(var p in craft.GetNodes("PART")){string name=CraftPartName(p);var info=PartLoader.getPartInfoByName(name);if(info!=null&&info.partPrefab!=null&&info.partPrefab.Modules.Contains("KDAEngine"))return name;}return "";}
 void OpenPrebuild(){
  prebuildCars.Clear();error="";prebuildScroll=Vector2.zero;
  var saved=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  string ships=Path.Combine(KSPUtil.ApplicationRootPath,"saves",HighLogic.SaveFolder,"Ships");
  foreach(string facility in new[]{"SPH","VAB"}){string folder=Path.Combine(ships,facility);if(!Directory.Exists(folder))continue;foreach(string path in Directory.GetFiles(folder,"*.craft",SearchOption.AllDirectories))try{string id=CraftBody(ConfigNode.Load(path));if(id!=""&&!saved.ContainsKey(id))saved.Add(id,path);}catch(Exception ex){Debug.LogWarning("[KDA Prebuild] "+ex.Message);}}
  var templates=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  string templateFolder=Path.Combine(KSPUtil.ApplicationRootPath,"GameData","KDAVehicles","Prebuilds");
  if(Directory.Exists(templateFolder))foreach(string path in Directory.GetFiles(templateFolder,"*.craft"))try{string id=CraftBody(ConfigNode.Load(path));if(id!="")templates[id]=path;}catch(Exception ex){Debug.LogWarning("[KDA Prebuild] Template: "+ex.Message);}
  foreach(var info in PartLoader.LoadedPartsList){if(!IsKDAPart(info)||!info.partPrefab.Modules.Contains("KDAEngine")||!PartAvailable(info))continue;string template,existing;templates.TryGetValue(info.name,out template);saved.TryGetValue(info.name,out existing);prebuildCars.Add(new PrebuildEntry{Body=info,Template=template,Saved=existing});}
  prebuildCars.Sort((a,b)=>String.Compare(a.Body.title,b.Body.title,StringComparison.OrdinalIgnoreCase));prebuildDialog=true;
 }
 void DrawPrebuild(){
  GUI.Label(new Rect(450,280,540,35),"PREBUILD CAR",heading);
  GUI.Label(new Rect(450,319,540,64),"Included cars use saved seat and doorway positions. Other imports get wheels and an outward door marker: check placement and add a seat. Existing saved cars are loaded, never duplicated.",small);
  prebuildScroll=GUI.BeginScrollView(new Rect(450,389,540,125),prebuildScroll,new Rect(0,0,515,Math.Max(110,prebuildCars.Count*47)));
  for(int i=0;i<prebuildCars.Count;i++){var entry=prebuildCars[i];if(Button(new Rect(0,i*47,510,41),(entry.Saved!=null?"LOAD SAVED: ":entry.Template!=null?"COMPLETE: ":"CHASSIS: ")+entry.Body.title,false))PrepareCar(entry);}
  GUI.EndScrollView();
  if(prebuildCars.Count==0)GUI.Label(new Rect(460,390,510,40),"No unlocked KDA car packages installed.",small);
  if(Button(new Rect(450,535,540,38),"CANCEL",false))CancelModal();
 }
 void PrepareCar(PrebuildEntry entry){
  try{
   // Recheck saves on every request, including a file created since opening this menu.
   string root=Path.Combine(KSPUtil.ApplicationRootPath,"saves",HighLogic.SaveFolder,"Ships");string path=null;
   foreach(string facility in new[]{"SPH","VAB"}){string folder=Path.Combine(root,facility);if(!Directory.Exists(folder))continue;foreach(string p in Directory.GetFiles(folder,"*.craft",SearchOption.AllDirectories)){try{if(CraftBody(ConfigNode.Load(p))==entry.Body.name){path=p;break;}}catch(Exception){}}if(path!=null)break;}
   if(path==null){
    bool approximate=false;ConfigNode craft=entry.Template!=null?ConfigNode.Load(entry.Template):BuildImportedChassis(entry.Body,out approximate);
    string missing="";if(craft==null||!ShipConstruction.AllPartsFound(craft,ref missing))throw new IOException("Prebuild requires missing parts: "+missing);
    string directory=ShipConstruction.GetCurrentGameShipsPathFor(EditorDriver.editorFacility);Directory.CreateDirectory(directory);
    string name=craft.GetValue("ship")??entry.Body.title;string stem=KDAEditorCraftNames.SaveStem(name,'_',true,directory);path=Path.Combine(directory,stem+".craft");
    for(int n=2;File.Exists(path);n++)path=Path.Combine(directory,stem+" ("+n+").craft");
    if(!craft.Save(path))throw new IOException("Prebuilt craft could not be saved.");
    status=entry.Template!=null?"Complete car created from its supplied template.":approximate?"Chassis created. Door position is approximate; check it and add a seat.":"Chassis created. Check the door marker and add a seat.";
   }
   var config=ConfigNode.Load(path);var target=new CarEntry{Path=path,Name=config.GetValue("ship")??entry.Body.title,Source="Your save",Parts=config.GetNodes("PART").Length};
   prebuildDialog=false;RefreshCars();loadRequest=target;
   // Use the existing load confirmation/recovery path before replacing editor work.
  }catch(Exception ex){error="Could not prebuild car: "+ex.Message;prebuildDialog=false;}
 }
 static string Vec(Vector3 v){return v.x.ToString("R",CultureInfo.InvariantCulture)+","+v.y.ToString("R",CultureInfo.InvariantCulture)+","+v.z.ToString("R",CultureInfo.InvariantCulture);}
 static string Rot(Quaternion q){return Vec(new Vector3(q.x,q.y,q.z))+","+q.w.ToString("R",CultureInfo.InvariantCulture);}
 static ConfigNode PrebuildPart(ConfigNode craft,string name,int id,Vector3 position,Quaternion rotation,Vector3 local,Quaternion relative,int parent){
  var node=craft.AddNode("PART");node.AddValue("part",name+"_"+id);node.AddValue("partName","Part");node.AddValue("persistentId",id);node.AddValue("pos",Vec(position));node.AddValue("rot",Rot(rotation));node.AddValue("attPos",Vec(local));node.AddValue("attPos0",Vec(local));node.AddValue("attRot",Rot(relative));node.AddValue("attRot0",Rot(relative));node.AddValue("mir","1,1,1");node.AddValue("symMethod","Mirror");node.AddValue("attachMode",0);node.AddValue("inverseStage",0);node.AddValue("parent",parent);return node;
 }
 ConfigNode BuildImportedChassis(AvailablePart bodyInfo,out bool approximate){
  var craft=new ConfigNode();craft.AddValue("ship",bodyInfo.title);craft.AddValue("version",Versioning.GetVersionString());craft.AddValue("type",EditorDriver.editorFacility.ToString());craft.AddValue("size","2,2,5");craft.AddValue("vesselType","Rover");
  Quaternion bodyRot=Quaternion.Euler(90,0,0);Vector3 origin=new Vector3(0,2,0);
  var bodyNode=PrebuildPart(craft,bodyInfo.name,100,origin,bodyRot,Vector3.zero,bodyRot,-1);
  string prefix=bodyInfo.name.EndsWith("-body",StringComparison.Ordinal)?bodyInfo.name.Substring(0,bodyInfo.name.Length-5):bodyInfo.name;
  bodyNode.AddValue("mass",bodyInfo.partPrefab.mass.ToString("R",CultureInfo.InvariantCulture));
  int id=101;float halfWidth=0;
  foreach(string corner in new[]{"FL","FR","RL","RR"}){
   var anchor=bodyInfo.partPrefab.attachNodes.Find(n=>n.id==corner);var wheel=PartLoader.getPartInfoByName(prefix+"-"+corner);
   if(anchor==null||wheel==null)throw new IOException("Missing wheel or attachment node "+corner+". This package cannot be prebuilt safely.");
   Vector3 local=anchor.position;halfWidth=Mathf.Max(halfWidth,Mathf.Abs(local.x));bool left=local.x<0;
   Quaternion rotation=left?new Quaternion(0,1,0,0):Quaternion.identity;string wheelId=wheel.name+"_"+id;
   var wheelNode=PrebuildPart(craft,wheel.name,id++,origin+bodyRot*local,rotation,local,Quaternion.Inverse(bodyRot)*rotation,0);
   wheelNode.AddValue("mass",wheel.partPrefab.mass.ToString("R",CultureInfo.InvariantCulture));bodyNode.AddValue("link",wheelId);bodyNode.AddValue("attN",corner+","+wheelId);wheelNode.AddValue("attN","top,"+bodyInfo.name+"_100");
  }
  Vector3 door=new Vector3(-halfWidth-.12f,0,-.45f);approximate=true;
  // Named door geometry is optional in Automation exports. Ignore handles/hinges and
  // use body-local coordinates; generic fallback is explicitly labelled approximate.
  foreach(var mesh in bodyInfo.partPrefab.GetComponentsInChildren<MeshFilter>(true)){
   string name=mesh.name.ToLowerInvariant();if(!name.Contains("door")||name.Contains("handle")||name.Contains("hinge")||mesh.sharedMesh==null)continue;
   Vector3 candidate=bodyInfo.partPrefab.transform.InverseTransformPoint(mesh.transform.TransformPoint(mesh.sharedMesh.bounds.center));
   if(Mathf.Abs(candidate.x)<halfWidth*.5f||candidate.x>0)continue;
   door=candidate;door.x=-Mathf.Max(halfWidth+.12f,Mathf.Abs(candidate.x)+.05f);approximate=false;break;
  }
  // Optional explicit package metadata takes precedence over inferred door geometry.
  foreach(var setup in GameDatabase.Instance.GetConfigNodes("KDA_PREBUILD"))if(setup.GetValue("part")==bodyInfo.name&&setup.HasValue("doorPosition")){
   door=ParsePosition(setup.GetValue("doorPosition"));if(float.IsNaN(door.x)||float.IsInfinity(door.x)||float.IsNaN(door.y)||float.IsInfinity(door.y)||float.IsNaN(door.z)||float.IsInfinity(door.z)||Mathf.Abs(door.x)<.05f)throw new IOException("Invalid door position in package prebuild metadata.");approximate=false;break;
  }
  if(PartLoader.getPartInfoByName("kda-doorway-marker")==null)throw new IOException("Install the KDA doorway marker first.");
  Vector3 outward=bodyRot*(door.x<0?Vector3.left:Vector3.right);Quaternion doorRot=Quaternion.LookRotation(outward,Vector3.up);
  string doorId="kda-doorway-marker_"+id;bodyNode.AddValue("link",doorId);
  var marker=PrebuildPart(craft,"kda-doorway-marker",id,origin+bodyRot*door,doorRot,door,Quaternion.Inverse(bodyRot)*doorRot,0);
  marker.AddValue("mass",PartLoader.getPartInfoByName("kda-doorway-marker").partPrefab.mass.ToString("R",CultureInfo.InvariantCulture));marker.SetValue("attachMode",1);marker.AddValue("srfN","srfAttach,"+bodyInfo.name+"_100");
  craft.AddValue("description",approximate?"Prebuilt chassis. Door location is approximate: check marker placement and outward arrow; add and position a KDA seat before driving.":"Prebuilt chassis. Check wheels and outward door marker; add and position a KDA seat before driving.");return craft;
 }
}

