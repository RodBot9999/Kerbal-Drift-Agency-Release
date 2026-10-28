using System;using System.Collections;using System.IO;using UnityEngine;using KSP.UI;

public sealed partial class KDAGarage {
 bool loadingCar;string loadingName="";Coroutine carLoadRoutine;
 public bool IsCarLoading {get{return loadingCar;}}
 IEnumerator LoadCarInGarage(string path){
  // Paint the loading state before stock construction blocks the main thread.
  // Never close/reopen the garage or restore the stock editor chrome here.
  yield return new WaitForEndOfFrame();yield return null;
  if(!open){FinishCarLoad();yield break;}
  bool loaded=LoadNativeCar(path);
  if(loaded){
   // Part OnStart and editor callbacks finish on the next frame. Keep the
   // existing panels/collection alive while their new craft bindings settle.
   yield return null;yield return new WaitForEndOfFrame();
   if(open&&EditorLogic.fetch!=null)CompleteNativeCar();
  }
  FinishCarLoad();
 }
 bool LoadNativeCar(string path){
  try{
   if(!File.Exists(path))throw new IOException("The selected craft file is no longer available.");
   EditorLogic.LoadShipFromFile(path);RememberCarPath(path);BindCraft();MaintainGarageCanvases();return true;
  }catch(Exception ex){error="Car was not loaded: "+ex.Message;if(EditorLogic.fetch!=null&&EditorLogic.fetch.ship!=null)BindCraft();return false;}
 }
 void CompleteNativeCar(){try{BindCraft();if(sph!=null){sph.minDistance=1.2f;sph.minHeight=0;}if(vab!=null){vab.minDistance=1.2f;vab.minHeight=0;}UpdatePreview();FrameCar();status="Loaded "+loadingName+".";}catch(Exception ex){error="Car loaded, but garage binding failed: "+ex.Message;}}
 void MaintainGarageCanvases(){
  var ui=UIMasterController.Instance;if(ui==null)return;
  foreach(var canvas in new[]{ui.mainCanvas,ui.appCanvas}){
   if(canvas==null)continue;bool known=false;foreach(var saved in hiddenCanvas)if(saved.Canvas==canvas){known=true;break;}
   if(!known)HideCanvas(canvas);else canvas.enabled=false;
  }
  MaintainGarageInput();
 }
 void FinishCarLoad(){loadingCar=false;loadingName="";carLoadRoutine=null;if(open&&EditorLogic.fetch!=null){MaintainGarageCanvases();UpdateNavigationLock();}}
 void DrawLoadingCar(){
  Rect car=CarRect;var r=new Rect(car.center.x-180,car.center.y-48,360,96);Box(r,PanelColor);Box(new Rect(r.x,r.y,360,3),Accent);
  GUI.Label(new Rect(r.x+18,r.y+17,324,30),"LOADING CAR",sectionStyle);GUI.Label(new Rect(r.x+18,r.y+48,324,32),loadingName,small);
 }
}
