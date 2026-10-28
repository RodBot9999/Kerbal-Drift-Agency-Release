using System;using System.Collections.Generic;using ModuleWheels;using UnityEngine;
public sealed partial class KDAGarage {
 int travelAxle,travelPose;string clearance="Preview wheel clearance before launch.";int clearanceKey;float nextClearance;
 readonly Dictionary<Transform,Vector3> wheelPreviewPositions=new Dictionary<Transform,Vector3>();
 static readonly string[] TravelFields={"suspensionKit","travelMm","targetSagPercent","bumpStopStart","bumpStopStrength","bypassDamping","droopDamping","trackExtensionMm"};
 void BindTravelFields(){
  if(body==null)return;foreach(var p in body.children){var su=p.FindModuleImplementing<KDASuspension>();if(su==null)continue;bool front=body.transform.InverseTransformPoint(p.transform.position).y>0;
   foreach(string name in TravelFields){var f=su.Fields[name];if(f==null)continue;float v=Convert.ToSingle(f.GetValue(su));var s=Find(front,name);var range=f.uiControlEditor as UI_FloatRange;
    if(s==null){s=new Setting{Name=name,Label=name,Unit="",Help="",Front=front,Value=v,Original=v,Text=Number(v),Min=range==null?0:range.minValue,Max=range==null?2:range.maxValue,Step=range==null?1:range.stepIncrement,Integer=name=="suspensionKit"};settings.Add(s);}else if(Mathf.Abs(s.Original-v)>.0001f)s.Mixed=true;s.Targets.Add(new Target{Module=su,Field=f});
   }
   var pivot=p.FindModelTransform("suspensionPivot");if(pivot!=null)wheelPreviewPositions[pivot]=pivot.localPosition;
  }
  SetTravelLabels();AdjustTravelDraftRanges();
 }
 void SetTravelLabels(){foreach(var s in settings){switch(s.Name){case "rideHeightMm":s.Help="Static body height; independent of wheel travel.";break;case "springRateNmm":s.Help="Effective wheel rate; calculator can suggest a value.";break;case "travelMm":s.Label="Total wheel travel";s.Unit="mm";s.Help="Full bump to full droop; separate from ride height.";break;case "targetSagPercent":s.Label="Sag ceiling for spring calculator";s.Unit="%";s.Help="Suggest adds 75% load reserve; actual sag is shown above.";break;case "bumpStopStart":s.Label="Bump stop begins";s.Unit="% travel";s.Help="Progressive resistance beyond this compression.";break;case "bumpStopStrength":s.Label="Bump stop strength";s.Help="Resistance rises smoothly near full bump.";break;case "bypassDamping":s.Label="Position damping gain";s.Help="More damping toward full compression.";break;case "droopDamping":s.Label="Extension limit damping";s.Help="Slows rebound as wheels approach full droop.";break;case "trackExtensionMm":s.Label="Track extension per side";s.Unit="mm";s.Help="Widens the long-travel kit; inspect body clearance.";break;}}}
 void AdjustTravelDraftRanges(){for(int i=0;i<2;i++){bool front=i==0,kit=DraftValue(front,"suspensionKit")>0;var spring=Find(front,"springRateNmm");if(spring!=null){spring.Min=kit?5:93.75f;spring.Step=kit?1:2.5f;}var height=Find(front,"rideHeightMm");if(height!=null){height.Max=kit?350:30;height.Step=kit?5:2;}}}
 float DraftTravel(bool front){return (float)LongTravelCore.Travel((int)DraftValue(front,"suspensionKit"),DraftValue(front,"travelMm"));}
 float DraftLoad(bool front){if(body==null)return 2.5f;foreach(var p in body.children){var s=p.FindModuleImplementing<KDASuspension>();if(s!=null&&(body.transform.InverseTransformPoint(p.transform.position).y>0)==front)return s.EditorCornerLoad();}return 2.5f;}
 public bool PreviewHardware(bool front,int mode){
  if(!open||loadingCar||body==null||mode<0||mode>2)return false;var kit=Find(front,"suspensionKit");if(kit==null)return false;Set(kit,mode,true);AdjustTravelDraftRanges();
  if(mode==0){var sc=body.FindModuleImplementing<KDAController>();var p=KDACore.Preset(sc==null?1:sc.chassisMode);DraftSet(front,"springRateNmm",(float)(front?p.FrontSpring:p.RearSpring));DraftSet(front,"rideHeightMm",(float)p.Height);DraftSet(front,"antiRollRate",(float)(front?p.FrontBar:p.RearBar));DraftSet(front,"bumpDamping",(float)p.Bump);DraftSet(front,"reboundDamping",(float)p.Rebound);DraftSet(front,"fastBumpDamping",(float)p.FastBump);DraftSet(front,"fastReboundDamping",(float)p.FastRebound);}
  else{bool desert=mode==2;DraftSet(front,"travelMm",desert?550:300);DraftSet(front,"rideHeightMm",desert?180:80);DraftSet(front,"targetSagPercent",desert?40:35);DraftSet(front,"trackExtensionMm",desert?150:0);DraftSet(front,"bumpStopStart",desert?75:80);DraftSet(front,"bumpStopStrength",desert?3.5f:3);DraftSet(front,"bypassDamping",desert?1.7f:1);DraftSet(front,"droopDamping",1);DraftSet(front,"antiRollRate",front?(desert?3:5):(desert?2:3));DraftSet(front,"bumpDamping",.65f);DraftSet(front,"reboundDamping",.9f);DraftSet(front,"fastBumpDamping",.7f);DraftSet(front,"fastReboundDamping",.8f);SuggestDraftSpring(front);}
  travelPose=0;UpdatePreview();return true;
 }
 void DraftSet(bool front,string name,float value){var s=Find(front,name);if(s!=null)Set(s,value,false);}
 public bool SuggestDraftSpring(bool front){if(!open||loadingCar||DraftValue(front,"suspensionKit")==0)return false;DraftSet(front,"springRateNmm",(float)LongTravelCore.SuggestedSpring(DraftLoad(front),DraftTravel(front),DraftValue(front,"targetSagPercent")));UpdatePreview();return true;}
 public void SetTravelPreview(int pose){travelPose=Mathf.Clamp(pose,0,3);UpdatePreview();}
 void RestoreTravelPreview(){foreach(var p in wheelPreviewPositions)if(p.Key!=null)p.Key.localPosition=p.Value;wheelPreviewPositions.Clear();if(body!=null)foreach(var v in body.FindModulesImplementing<KDASuspensionVisual>())v.StopGarageRig();travelPose=0;}
 // Cars presents the applied setup. Tune may show drafts and travel poses;
 // Build keeps native model pivots so placement gizmos retain their reference.
 float DisplayedSuspensionValue(bool front,string name){var s=Find(front,name);return s==null?0:stockPresentation||view==GarageView.Cars?s.Original:s.Value;}
 // Closed garage: keep only applied cosmetic stance/hardware in the stock
 // editor. Never move Part transforms, attachment nodes or physics colliders.
 // Refresh from live modules so native tweakables, undo and craft loads work.
 void UpdateStockPresentation(){
  if(destroying)return;
  if(!HighLogic.LoadedSceneIsEditor||EditorLogic.fetch==null||EditorLogic.fetch.ship==null){
   if(stockPresentation){RestoreTravelPreview();if(model!=null){model.localPosition=modelPosition;model.localRotation=modelRotation;}model=null;stockPresentation=false;}return;
  }
  if(!stockPresentation||EditorLogic.fetch.ship!=ship||ship.parts.Count!=partCount||FindBody()!=body){stockPresentation=true;BindCraft();}
  if(body==null)return;
  foreach(var s in settings){bool first=true;foreach(var t in s.Targets){if(t.Module==null)continue;float v=Convert.ToSingle(t.Field.GetValue(t.Module));if(first){s.Value=s.Original=v;s.Text=Number(v);s.Mixed=false;s.Edited=false;first=false;}else if(Mathf.Abs(v-s.Original)>.0001f)s.Mixed=true;}}
  travelPose=0;UpdatePreview();
 }
 void UpdateTravelPreview(){
  if(body==null)return;foreach(var p in body.children){var su=p.FindModuleImplementing<KDASuspension>();if(su==null)continue;var pivot=p.FindModelTransform("suspensionPivot");Vector3 original;if(pivot==null||!wheelPreviewPositions.TryGetValue(pivot,out original))continue;
   bool front=body.transform.InverseTransformPoint(p.transform.position).y>0;int kit=(int)DisplayedSuspensionValue(front,"suspensionKit");float track=DisplayedSuspensionValue(front,"trackExtensionMm"),height=DisplayedSuspensionValue(front,"rideHeightMm");float travel=(float)LongTravelCore.Travel(kit,DisplayedSuspensionValue(front,"travelMm")),rate=DisplayedSuspensionValue(front,"springRateNmm"),sag=(float)LongTravelCore.StaticDepth(DraftLoad(front),rate,travel),side=Mathf.Sign(body.transform.InverseTransformPoint(p.transform.position).x);
   int pose=!stockPresentation&&view==GarageView.Tune?travelPose:0;float stroke=pose==1?sag-travel:pose==2?sag:pose==3?(side<0?sag-travel:sag):0;
   bool enabled=stockPresentation||view!=GarageView.Build;float lateral=enabled&&kit>0?(float)LongTravelCore.Lateral(travel,height,sag,sag-stroke,track)*side:0;
   pivot.localPosition=original+pivot.parent.InverseTransformVector(body.transform.forward*(enabled?stroke:0)+body.transform.right*lateral);
   foreach(var v in body.FindModulesImplementing<KDASuspensionVisual>())v.UpdateGarageRig(p,pivot,enabled&&kit>0,travel,kit,track);
  }
  CheckTravelClearance();
 }
 public string ClearanceStatus {get{return clearance;}}
 public string TravelSetupIssue {get{for(int axle=0;axle<2;axle++){bool front=axle==0;if(DraftValue(front,"suspensionKit")==0)continue;float travel=DraftTravel(front),sag=(float)LongTravelCore.StaticDepth(DraftLoad(front),DraftValue(front,"springRateNmm"),travel);if(sag>=travel*.8f)return (front?"Front":"Rear")+": too little bump travel; use a stronger spring.";float reach=(float)LongTravelCore.ArmLength(travel)+DraftValue(front,"trackExtensionMm")*.001f;if(DraftValue(front,"rideHeightMm")*.001f+sag>reach*.96f)return (front?"Front":"Rear")+": full droop exceeds arm reach; reduce height or add track width.";}return "";}}
 void CheckTravelClearance(){
  if(!open||Time.realtimeSinceStartup<nextClearance||view!=GarageView.Tune)return;nextClearance=Time.realtimeSinceStartup+.5f;int key=body.GetInstanceID();foreach(var s in settings)key=unchecked(key*31+s.Value.GetHashCode());if(key==clearanceKey)return;clearanceKey=key;
  int overlaps=0;foreach(var p in body.children){var su=p.FindModuleImplementing<KDASuspension>();if(su==null)continue;bool front=body.transform.InverseTransformPoint(p.transform.position).y>0;if(DraftValue(front,"suspensionKit")==0)continue;var wb=p.FindModuleImplementing<ModuleWheelBase>();float r=wb==null?.3f:wb.radius,t=DraftTravel(front),sag=(float)LongTravelCore.StaticDepth(DraftLoad(front),DraftValue(front,"springRateNmm"),t),side=Mathf.Sign(body.transform.InverseTransformPoint(p.transform.position).x);
   var pivot=p.FindModelTransform("suspensionPivot");Vector3 original;if(pivot==null||!wheelPreviewPositions.TryGetValue(pivot,out original))continue;Vector3 hub=pivot.parent.TransformPoint(original);
   var steering=p.FindModuleImplementing<ModuleWheelSteering>();var sc=body.FindModuleImplementing<KDAController>();float lockAngle=steering!=null&&steering.steeringEnabled?Mathf.Min(85,Mathf.Max(steering.steerAngle,sc!=null&&sc.driveMode==3?sc.driftSteeringLock+15:0)):0;
   for(int i=0;i<=8;i++){float d=t*i/8,lat=side*(float)LongTravelCore.Lateral(t,DraftValue(front,"rideHeightMm"),sag,d,DraftValue(front,"trackExtensionMm"));Vector3 at=hub+body.transform.forward*(sag-d)+body.transform.right*lat;
    for(int steer=lockAngle>0?-1:0;steer<=1;steer++){foreach(var hit in Physics.OverlapBox(at,new Vector3(r*.38f,r*.92f,r*.92f),body.transform.rotation*Quaternion.AngleAxis(steer*lockAngle,Vector3.back),~0,QueryTriggerInteraction.Ignore))if(hit!=null&&hit.GetComponentInParent<Part>()==body){overlaps++;break;}if(lockAngle==0)break;}
   }
  }
  clearance=overlaps>0?"Body overlap in travel / steering sweep. Adjust height, travel or track; inspect full bump.":"No body overlap in sampled travel / steering sweep. Approximate tire boxes; inspect visually.";
 }
 void DrawTravelTuning(){
  if(Button(new Rect(29,310,181,32),"FRONT AXLE",travelAxle==0))travelAxle=0;if(Button(new Rect(222,310,181,32),"REAR AXLE",travelAxle==1))travelAxle=1;bool front=travelAxle==0;int kit=(int)DraftValue(front,"suspensionKit");
  scroll=GUI.BeginScrollView(new Rect(24,352,406,432),scroll,new Rect(0,0,380,1160));string[] modes={"STANDARD","RALLY 300","DESERT 550"};for(int i=0;i<3;i++)if(Button(new Rect(4+i*126,0,118,35),modes[i],kit==i))PreviewHardware(front,i);
  float t=DraftTravel(front),load=DraftLoad(front),k=DraftValue(front,"springRateNmm"),sag=(float)LongTravelCore.StaticDepth(load,k,t);
  GUI.Label(new Rect(4,44,370,56),"Travel "+(t*1000).ToString("0")+" mm · estimated sag "+(sag/t*100).ToString("0")+"%\nDroop "+(sag*1000).ToString("0")+" / bump "+(Mathf.Max(0,t-sag)*1000).ToString("0")+" mm",small);
  bool old=GUI.enabled;GUI.enabled=old&&kit!=0;if(Button(new Rect(4,109,370,34),"SUGGEST STIFFER OFF-ROAD SPRING",false))SuggestDraftSpring(front);
  string[] fields={"travelMm","rideHeightMm","targetSagPercent","springRateNmm","trackExtensionMm","bumpStopStart","bumpStopStrength","bypassDamping","droopDamping"};for(int i=0;i<fields.Length;i++){var s=Find(front,fields[i]);if(s!=null)Row(s,160+i*92);}GUI.enabled=old;
  GUI.Label(new Rect(4,1002,370,135),"Suggest uses weight + 75% load reserve. Sag ceiling is a calculator input; actual estimated sag is shown above. Changing the input alone does not change the spring. Rate limit: 280 N/mm. Apply then Save. Desert hardware still needs body clearance.",small);GUI.EndScrollView();
  Rect car=CarRect;string[] poses={"RIDE","FULL BUMP","FULL DROOP","ARTICULATE"};for(int i=0;i<4;i++)if(Button(new Rect(car.x+20+i*120,car.yMax-126,112,31),poses[i],travelPose==i))SetTravelPreview(i);
  GUI.Label(new Rect(car.x+20,car.yMax-91,car.width-40,38),clearance,small);
 }
}
