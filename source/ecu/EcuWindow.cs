// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class KDAECU {
 bool windowOpen,windowLocked;Vector2 wheelScroll;Rect setupRect=new Rect(240,130,790,530);
 string LockName {get{return "KDA.ECU."+GetInstanceID();}}
 internal float SteeringCentreX {get{return leftSteering<0||rightSteering<0?0:(Local(Wheels[leftSteering].Part).x+Local(Wheels[rightSteering].Part).x)*.5f;}}
 void OnGUI(){
  if(!windowOpen||(!HighLogic.LoadedSceneIsEditor&&!HighLogic.LoadedSceneIsFlight)){UnlockSetup();return;}
  setupRect.width=Mathf.Min(790,Screen.width-20);setupRect.height=Mathf.Min(530,Screen.height-20);
  setupRect.x=Mathf.Clamp(setupRect.x,0,Screen.width-setupRect.width);setupRect.y=Mathf.Clamp(setupRect.y,0,Screen.height-setupRect.height);
  setupRect=GUI.Window(GetInstanceID(),setupRect,DrawSetup,"KDA - ROVER DRIVE-BY-WIRE ECU");
  Vector2 mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
  if(setupRect.Contains(mouse)&&!windowLocked){InputLockManager.SetControlLock(ControlTypes.ALLBUTCAMERAS,LockName);windowLocked=true;}
  else if(!setupRect.Contains(mouse))UnlockSetup();
 }
 void UnlockSetup(){if(windowLocked){InputLockManager.RemoveControlLock(LockName);windowLocked=false;}}
 void DrawSetup(int id){
  float width=setupRect.width;GUI.Label(new Rect(16,27,width-32,42),"Mount the ECU flat with its arrow pointing forwards and its top facing up.\nON: exactly two front steering wheels. OFF: native wheel settings are restored.");
  if(GUI.Button(new Rect(width-78,3,68,22),"CLOSE")){windowOpen=false;UnlockSetup();}
  if(GUI.Button(new Rect(16,77,235,31),ecuEnabled?"ECU / SC: ON":"ECU / SC: OFF"))ToggleEcu();
  if(GUI.Button(new Rect(266,77,205,31),"REFRESH WHEELS")){Release();nextScan=0;}
  GUI.Label(new Rect(16,116,width-32,52),"Unstable / Normal profiles, adaptive steering, yaw brake support and ABS.\nNative motors, resource use, suspension and tire grip are always retained.");
  GUI.Label(new Rect(16,174,width-32,25),"WHEEL                                      GROUP");
  var candidates=new List<Part>();IEnumerable<Part> parts=HighLogic.LoadedSceneIsEditor&&EditorLogic.fetch!=null&&EditorLogic.fetch.ship!=null?EditorLogic.fetch.ship.parts:vessel==null?new List<Part>():vessel.parts;
  foreach(var p in parts)if(p.FindModuleImplementing<ModuleWheelBase>()!=null)candidates.Add(p);
  float visible=Mathf.Max(90,setupRect.height-268);wheelScroll=GUI.BeginScrollView(new Rect(12,204,width-24,visible),wheelScroll,new Rect(0,0,Mathf.Max(660,width-48),Mathf.Max(visible,candidates.Count*42)));
  for(int i=0;i<candidates.Count;i++){
   var p=candidates[i];var c=Choice(p);string title=p.partInfo==null?p.name:p.partInfo.title;
   GUI.Label(new Rect(6,i*42,300,34),title+" #"+p.craftID+"\n"+Local(p).z.ToString("F2")+"m forward / "+Local(p).x.ToString("F2")+"m right");
   GUI.enabled=HighLogic.LoadedSceneIsEditor;
   if(GUI.Button(new Rect(310,i*42,150,34),new[]{"AUTO","FRONT","REAR","EXCLUDE"}[c.Group])){c.Group=(c.Group+1)%4;nextScan=0;}

  }
  GUI.enabled=true;GUI.EndScrollView();GUI.Label(new Rect(16,setupRect.height-54,width-32,46),HighLogic.LoadedSceneIsEditor?"Save the craft to keep ECU tuning and wheel groups. Front pair: one steering-capable wheel on each side.\nAdditional axles may be driven; they do not steer while the ECU is ON.":ecuStatus+"\nWheel groups are editor-only. Toggle ECU OFF to restore the native rover.");
  GUI.DragWindow(new Rect(0,0,width-90,24));
 }
}
