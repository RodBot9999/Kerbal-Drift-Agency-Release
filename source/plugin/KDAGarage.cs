using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Editor workbench. Drafts never alter the craft until Apply; the model-only
// stance preview is restored before closing, changing ships or leaving SPH/VAB.
[KSPAddon(KSPAddon.Startup.EditorAny,false)]
public sealed partial class KDAGarage:MonoBehaviour {
 public static KDAGarage Instance {get;private set;}
 const string LockName="KDA.Garage";
 public const float TravelMm=160;
 sealed class Target {public PartModule Module;public BaseField Field;}
 sealed class Setting {
  public string Name,Label,Unit,Help,Text;public bool Front,Mixed,Edited,Integer;
  public float Value,Original,Min,Max,Step;
  public readonly List<Target> Targets=new List<Target>();
  public bool Dirty {get{return Edited&&(Mixed||Mathf.Abs(Value-Original)>.00001f);}}
  public bool Valid {get{float parsed;return float.TryParse(Text,NumberStyles.Float,CultureInfo.InvariantCulture,out parsed)&&!float.IsNaN(parsed)&&!float.IsInfinity(parsed)&&parsed>=Min&&parsed<=Max&&(!Integer||parsed==Mathf.Round(parsed));}}
 }
 readonly List<Setting> settings=new List<Setting>();
 readonly List<KDAAxle> links=new List<KDAAxle>();
 Part body;ShipConstruct ship;int partCount,section,graphAxle;
 Transform model;Vector3 modelPosition;Quaternion modelRotation;
 bool open,locked,cameraLocked,stockPresentation,destroying;string status="";Vector2 scroll;
 float graphStroke=.35f,graphDamperSpeed=.25f;
 Texture2D pixel;GUIStyle heading,label,small,valueStyle,button,field,sectionStyle;
 public bool IsOpen {get{return open;}}
 public void ReportWorldStatus(string text){status=text;}
 public Part Body {get{return body;}}
 public int DrawFrames {get;private set;}
 public int PendingChanges {get{int count=ExhaustDirty?1:0;foreach(var s in settings)if(s.Dirty)count++;return count;}}
 public int SettingCount {get{return settings.Count;}}
 public bool ValidInputs {get{foreach(var s in settings)if(!s.Valid)return false;return TravelSetupIssue.Length==0;}}
 public float PreviewHeaveMm {get;private set;}
 public float PreviewPitchDegrees {get;private set;}
 static readonly string[] Names={"rideHeightMm","springRateNmm","antiRollRate","toeIn","bumpDamping","reboundDamping","fastBumpDamping","fastReboundDamping"};
 static readonly string[] Labels={"Ride height offset","Spring rate","Anti-roll rate","Toe per wheel","Compression","Rebound","Fast compression","Fast rebound"};
 static readonly string[] Units={"mm","N/mm","N/mm","deg","ratio","ratio","ratio","ratio"};
 static readonly string[] Help={"Visual stance preview. Applies the existing ride-height setting in flight.","Spring force gained for each millimetre of compression.","Links left/right wheel compression to resist body roll.","Positive values point the fronts of the tires inward.","Damping during slow compression.","Damping as the wheel extends after compression.","Compression damping at high shaft speed; useful for sharp impacts.","Extension damping at high shaft speed."};
 static readonly Color Accent=new Color(.24f,.86f,.77f),Orange=new Color(1,.66f,.30f);
 void Awake(){Instance=this;}
 Part FindBody(){if(EditorLogic.fetch==null||EditorLogic.fetch.ship==null)return null;foreach(var p in EditorLogic.fetch.ship.parts)if(p!=null&&p.FindModuleImplementing<KDAEngine>()!=null)return p;return null;}
 public bool Open(){
  if(!HighLogic.LoadedSceneIsEditor||EditorLogic.fetch==null)return false;
  if(open)Close();stockPresentation=false;BindCraft();
  open=true;section=graphAxle=0;scroll=Vector2.zero;OpenEditor();UpdatePreview();FrameCar();return true;
 }
 void BindCraft(){
  RestoreTravelPreview();
  if(model!=null){model.localPosition=modelPosition;model.localRotation=modelRotation;}
  body=FindBody();model=null;PreviewHeaveMm=PreviewPitchDegrees=0;
  ship=EditorLogic.fetch.ship;partCount=ship.parts.Count;settings.Clear();links.Clear();statsBody=null;BindExhaust();BindExtraTuning();BindNitrous();
  if(body==null)return;
  foreach(var p in body.children){var suspension=p.FindModuleImplementing<KDASuspension>();if(suspension==null)continue;bool front=body.transform.InverseTransformPoint(p.transform.position).y>0;
   var link=p.FindModuleImplementing<KDAAxle>();if(link!=null)links.Add(link);
   for(int i=0;i<Names.Length;i++){
    var f=suspension.Fields[Names[i]];var range=f==null?null:f.uiControlEditor as UI_FloatRange;
    if(f==null||range==null||!f.guiActiveEditor)continue;float current=Convert.ToSingle(f.GetValue(suspension),CultureInfo.InvariantCulture);
    Setting s=Find(front,Names[i]);if(s==null){s=new Setting{Name=Names[i],Label=Labels[i],Unit=Units[i],Help=Help[i],Front=front,Value=current,Original=current,Min=range.minValue,Max=range.maxValue,Step=Mathf.Max(.001f,range.stepIncrement)};s.Text=Number(current);settings.Add(s);}else if(Mathf.Abs(s.Original-current)>.00001f)s.Mixed=true;
    s.Targets.Add(new Target{Module=suspension,Field=f});
   }
  }
  BindTireGrip();BindTravelFields();model=body.transform.Find("model");if(model!=null){modelPosition=model.localPosition;modelRotation=model.localRotation;}
  status=settings.Count>0?"Preview changes, then Apply to car. Save the craft normally in the editor.":"This car has no supported adjustable suspension.";
 }
 public void Close(){
  RestoreTravelPreview();
  if(carLoadRoutine!=null)StopCoroutine(carLoadRoutine);carLoadRoutine=null;loadingCar=false;loadingName="";
  if(model!=null){model.localPosition=modelPosition;model.localRotation=modelRotation;}
  model=null;open=false;PreviewHeaveMm=PreviewPitchDegrees=0;CloseEditor();
  if(locked&&EditorLogic.fetch!=null)EditorLogic.fetch.Unlock(LockName);locked=false;if(Event.current!=null)GUIUtility.keyboardControl=0;
  stockPresentation=false;if(!destroying)UpdateStockPresentation();
 }
 Setting Find(bool front,string name){foreach(var s in settings)if(s.Front==front&&s.Name==name)return s;return null;}
 public float DraftValue(bool front,string name){var s=Find(front,name);return s==null?0:s.Value;}
 public bool PreviewValue(bool front,string name,float input){var s=Find(front,name);if(!open||loadingCar||s==null||float.IsNaN(input)||float.IsInfinity(input))return false;Set(s,input,false);UpdatePreview();return true;}
 static string Number(float v){return v.ToString("0.###",CultureInfo.InvariantCulture);}
 void Set(Setting s,float input,bool snap){float v=Mathf.Clamp(input,s.Min,s.Max);if(snap)v=Mathf.Clamp(s.Min+Mathf.Round((v-s.Min)/s.Step)*s.Step,s.Min,s.Max);s.Value=v;s.Text=Number(v);s.Edited=true;}
 public void DiscardPreview(){foreach(var s in settings){s.Value=s.Original;s.Text=Number(s.Value);s.Edited=false;}AdjustTravelDraftRanges();travelPose=0;exhaustDraft=exhaustOriginal;status="Unapplied changes discarded.";UpdatePreview();}
 public bool ApplyPreview(){
  if(!open||loadingCar||!ValidInputs||EditorLogic.fetch==null||EditorLogic.fetch.ship!=ship||body==null)return false;
  var targets=new List<Target>();var before=new List<object>();
  try{
   foreach(var s in settings)if(s.Dirty)foreach(var t in s.Targets){targets.Add(t);before.Add(t.Field.GetValue(t.Module));t.Field.SetValue(s.Integer||s.Name=="afterfireTune"?(object)(int)s.Value:s.Value,t.Module);}
   if(ExhaustDirty){targets.Add(exhaustTarget);before.Add(exhaustTarget.Field.GetValue(exhaustTarget.Module));exhaustTarget.Field.SetValue(exhaustDraft,exhaustTarget.Module);}
   foreach(var p in body.children){var su=p.FindModuleImplementing<KDASuspension>();if(su!=null)su.AcceptGarageSetup();}foreach(var link in links)if(link!=null)link.Capture();
  }catch(Exception error){for(int i=0;i<targets.Count;i++)targets[i].Field.SetValue(before[i],targets[i].Module);foreach(var link in links)if(link!=null)link.Capture();status="Could not apply tuning: "+error.Message;return false;}
  foreach(var s in settings)if(s.Dirty){s.Original=s.Value;s.Mixed=false;s.Edited=false;}
  exhaustOriginal=exhaustDraft;
  EditorLogic.fetch.SetBackup();status="Applied to this craft. Use the editor Save button to keep the setup.";return true;
 }
 void Update(){
  if(!open){UpdateStockPresentation();return;}
  if(loadingCar){if(!HighLogic.LoadedSceneIsEditor){Close();return;}if(EditorLogic.fetch!=null){MaintainGarageCanvases();UpdateGarageProjection();UpdateLock();}return;}
  if(!HighLogic.LoadedSceneIsEditor||EditorLogic.fetch==null||EditorLogic.fetch.ship==null){Close();return;}
  if(EditorLogic.fetch.ship!=ship||ship.parts.Count!=partCount||(body!=null&&!ship.parts.Contains(body)))BindCraft();
  UpdatePreview();UpdateEditor();UpdateLock();
 }
 void UpdateLock(){
  Vector2 mouse=GarageMouse();bool hover=loadingCar||ModalOpen||editingText||!CarRect.Contains(mouse)||mouse.y<CarRect.y+(view==GarageView.Cars?62:108)||mouse.y>CarRect.yMax-62;
  EditorLogic.fetch.Lock(hover,true,view!=GarageView.Build||hover,LockName);locked=true;cameraLocked=hover;
  // EditorLogic.Lock always includes CAMERACONTROLS, regardless of its load/
  // exit/save arguments. Let the native SPH/VAB camera handle right-drag and
  // zoom in the car viewport without releasing placement or UI protections.
  if(!hover){
   var controls=InputLockManager.GetControlLock(LockName)&~ControlTypes.CAMERACONTROLS;
   // Stock partRotationInputUpdate gates WASDQE and fine rotation on this
   // flag. Let KSP rotate the selected part, including its normal undo/symmetry.
   if(view==GarageView.Build)controls&=~ControlTypes.EDITOR_GIZMO_TOOLS;
   InputLockManager.SetControlLock(controls,LockName);
  }
  UpdateNavigationLock();
 }
 void UpdatePreview(){
  if((!open&&!stockPresentation)||body==null||model==null)return;
  if(!stockPresentation&&view==GarageView.Build){model.localPosition=modelPosition;model.localRotation=modelRotation;PreviewHeaveMm=PreviewPitchDegrees=0;UpdateTravelPreview();return;}
  float frontY=0,rearY=0;int nf=0,nr=0;
  foreach(var p in body.children)if(p.FindModuleImplementing<KDASuspension>()!=null){float y=body.transform.InverseTransformPoint(p.transform.position).y;if(y>0){frontY+=y;nf++;}else{rearY+=y;nr++;}}
  if(nf==0||nr==0)return;frontY/=nf;rearY/=nr;float wheelbase=frontY-rearY;if(wheelbase<.1f)return;
  float front=DisplayedSuspensionValue(true,"rideHeightMm")*.001f,rear=DisplayedSuspensionValue(false,"rideHeightMm")*.001f;
  float slope=(front-rear)/wheelbase;float height=rear-slope*rearY;PreviewHeaveMm=height*1000;PreviewPitchDegrees=-Mathf.Atan(slope)*Mathf.Rad2Deg;
  model.localPosition=modelPosition+new Vector3(0,0,-height);model.localRotation=Quaternion.AngleAxis(PreviewPitchDegrees,Vector3.right)*modelRotation;
  UpdateTravelPreview();
 }
 void Styles(){
  pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();
  label=new GUIStyle(GUI.skin.label){fontSize=15,alignment=TextAnchor.MiddleLeft,wordWrap=true};label.normal.textColor=new Color(.92f,.95f,.97f);
  small=new GUIStyle(label){fontSize=12};heading=new GUIStyle(label){fontSize=25,fontStyle=FontStyle.Bold};valueStyle=new GUIStyle(label){alignment=TextAnchor.MiddleRight,fontSize=16};
  button=new GUIStyle(GUI.skin.button){fontSize=14,fontStyle=FontStyle.Bold,wordWrap=true};field=new GUIStyle(GUI.skin.textField){fontSize=15,alignment=TextAnchor.MiddleCenter};sectionStyle=new GUIStyle(label){fontSize=14,fontStyle=FontStyle.Bold};sectionStyle.normal.textColor=Accent;
 }
 void Box(Rect r,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(r,pixel);GUI.color=old;}
 void Line(Vector2 a,Vector2 b,Color color,float width){var old=GUI.matrix;float length=(b-a).magnitude;GUI.matrix=old*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);Box(new Rect(0,-width*.5f,length,width),color);GUI.matrix=old;}
 bool Button(Rect r,string text,bool selected){var old=GUI.backgroundColor;GUI.backgroundColor=selected?Accent:new Color(.30f,.37f,.43f);bool clicked=GUI.Button(r,text,button);GUI.backgroundColor=old;return clicked;}
 void OnGUI(){
  if(!HighLogic.LoadedSceneIsEditor)return;if(!loadingCar&&KSP.UI.UIMasterController.Instance!=null&&!KSP.UI.UIMasterController.Instance.IsUIShowing)return;
  if(pixel==null)Styles();
  if(!open)return;
  var oldMatrix=GUI.matrix;var oldColor=GUI.color;GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
  try{DrawEditor();editingText=GUI.GetNameOfFocusedControl().StartsWith("KDA.Text.",StringComparison.Ordinal);}finally{GUI.matrix=oldMatrix;GUI.color=oldColor;}
  if(CaptureNitrousKey())return;
  if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){if(!loadingCar){if(ModalOpen)CancelModal();else if(KDAWorldGarage.ActiveSession)KDAWorldGarage.Instance.ExitEditor();else Close();}Event.current.Use();}
 }
 void DrawTuning(){
  if(body==null){GUI.Label(new Rect(25,180,380,100),"Load a KDA car from Cars to tune it.",label);return;}
  DrawPresets();string[] tabs={"SPRINGS","DAMPERS","ROLL / TOE","BRAKES","SOUND / FX","TRAVEL / KIT","NITROUS","TIRES"};for(int i=0;i<8;i++)if(Button(new Rect(25+(i%4)*103,223+(i/4)*38,99,32),tabs[i],section==i)){section=i;travelPose=0;scroll=Vector2.zero;GUIUtility.keyboardControl=0;}
  if(section==7)DrawTireGrip();else if(section==6)DrawNitrous();else if(section==5)DrawTravelTuning();else if(section==4)DrawExhaust();else if(section==3)DrawBrakeTuning();else{
  scroll=GUI.BeginScrollView(new Rect(24,310,406,474),scroll,new Rect(0,0,380,section==1?830:480));
  float y=4;for(int axle=0;axle<2;axle++){
   bool front=axle==0;GUI.Label(new Rect(4,y,370,28),front?"FRONT AXLE":"REAR AXLE",sectionStyle);y+=34;
   foreach(var s in settings){if(s.Front!=front||!InSection(s.Name))continue;Row(s,y);y+=87;}
   y+=20;
  }
  GUI.EndScrollView();
  }
  if(!showGraphs)return;
  Box(new Rect(1076,100,364,730),PanelColor);
  if(Button(new Rect(1094,111,97,29),"SPRING",graphPage==0))graphPage=0;if(Button(new Rect(1200,111,97,29),"DAMPING",graphPage==2))graphPage=2;if(Button(new Rect(1306,111,97,29),"BODY ROLL",graphPage==1))graphPage=1;
  if(graphPage==1){DrawAnalysis();return;}
  if(Button(new Rect(1094,147,150,32),"FRONT",graphAxle==0))graphAxle=0;if(Button(new Rect(1252,147,150,32),"REAR",graphAxle==1))graphAxle=1;
  if(graphPage==2){DrawDampingGraphs();return;}
  Graphs();
  GUI.Label(new Rect(1094,684,310,25),"Wheel travel: "+(DraftTravel(graphAxle==0)*1000).ToString("0")+" mm",label);
  GUI.Label(new Rect(1094,714,310,67),"Stance is a model preview. Curves describe the current spring and damping model; they are not a measured roll or handling prediction.",small);
 }
 bool InSection(string name){return section==0?name=="rideHeightMm"||name=="springRateNmm":section==2?name=="antiRollRate"||name=="toeIn":name=="bumpDamping"||name=="reboundDamping"||name=="fastBumpDamping"||name=="fastReboundDamping";}
 void Row(Setting s,float y){
  GUI.Label(new Rect(4,y,250,25),s.Label+(s.Mixed&&!s.Edited?" (mixed)":""),label);GUI.Label(new Rect(267,y,108,25),s.Unit,valueStyle);
  if(Button(new Rect(4,y+29,32,27),"−",false))Set(s,s.Value-s.Step,true);
  float v=GUI.HorizontalSlider(new Rect(44,y+39,184,16),s.Value,s.Min,s.Max);if(Mathf.Abs(v-s.Value)>.00001f)Set(s,v,true);
  var old=GUI.color;if(!s.Valid)GUI.color=new Color(1,.5f,.4f);GUI.SetNextControlName("KDA.Text."+s.Front+s.Name);string text=GUI.TextField(new Rect(237,y+29,96,27),s.Text,field);GUI.color=old;if(text!=s.Text){s.Text=text;float parsed;if(float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out parsed)&&!float.IsNaN(parsed)&&!float.IsInfinity(parsed)){s.Value=Mathf.Clamp(parsed,s.Min,s.Max);s.Edited=true;}}
  if(Button(new Rect(341,y+29,32,27),"+",false))Set(s,s.Value+s.Step,true);
  GUI.Label(new Rect(4,y+59,370,25),s.Help,small);
 }
 void Axes(Rect r,float ymax,string top,string bottom){
  GUI.Label(new Rect(r.x,r.y-29,r.width,25),top,label);Box(r,new Color(.06f,.08f,.10f));
  for(int i=0;i<=4;i++){float y=r.yMax-r.height*i/4;Line(new Vector2(r.x,y),new Vector2(r.xMax,y),new Color(.18f,.23f,.27f),1);GUI.Label(new Rect(r.x+3,Mathf.Clamp(y-19,r.y+2,r.yMax-20),65,20),(ymax*i/4).ToString("0.##",CultureInfo.InvariantCulture),small);}
  GUI.Label(new Rect(r.x,r.yMax+4,r.width,23),bottom,small);
 }
 void Graphs(){
  bool front=graphAxle==0;float spring=DraftValue(front,"springRateNmm"),travel=DraftTravel(front);bool longKit=DraftValue(front,"suspensionKit")>0;var r=new Rect(1094,226,308,142);float max=Mathf.Max(1,spring*travel*(longKit?1+.5f*DraftValue(front,"bumpStopStrength"):1));
  Axes(r,max,longKit?"Spring + bump stop force (kN)":"Added spring force (kN)","0                  compression                  "+(travel*1000).ToString("0")+" mm");
  Vector2 prev=Vector2.zero;for(int i=0;i<=80;i++){float d=travel*i/80;float force=longKit?(float)(spring*Math.Max(0,d-.1*travel)+LongTravelCore.BumpForce(spring,travel,d,DraftValue(front,"bumpStopStart"),DraftValue(front,"bumpStopStrength"))):spring*d;Vector2 pt=new Vector2(r.x+r.width*i/80,r.yMax-force/max*r.height);if(i>0)Line(prev,pt,Accent,2);prev=pt;}
  GUI.Label(new Rect(1094,402,308,24),"Spring: "+Number(spring)+" N/mm",small);
  float sag=(float)LongTravelCore.StaticDepth(DraftLoad(front),spring,travel);GUI.Label(new Rect(1094,434,308,54),"Estimated parked sag: "+(sag/travel*100).ToString("0.0")+"%\nBump travel left: "+(Mathf.Max(0,travel-sag)*1000).ToString("0")+" mm",small);
  if(Button(new Rect(1094,505,308,35),"OPEN DAMPING GRAPHS",false))graphPage=2;
  GUI.Label(new Rect(1094,554,308,97),"Spring = support as the wheel compresses.\nDamper = resistance while the wheel moves.\nThe Damping tab shows compression and rebound separately, in newtons.",small);
 }
 // Same coefficient, supported corner mass and position gain as flight.
 // Display the resisting force magnitude in N rather than a normalized ratio.
 public float DampingForcePreview(bool front,bool compression,float speed){
  speed=Mathf.Clamp(speed,0,1);bool kit=DraftValue(front,"suspensionKit")>0;double rate=LongTravelCore.Rate(kit?1:0,DraftValue(front,"springRateNmm")),mass=Math.Max(.01,DraftLoad(front)/9.80665),velocity=compression?speed:-Math.Max(.000001,speed);
  double coefficient=SuspensionCore.Damper(rate,mass,velocity,Mathf.Clamp(DraftValue(front,"bumpDamping"),.30f,2.1f),Mathf.Clamp(DraftValue(front,"reboundDamping"),.45f,2.6f),Mathf.Clamp(DraftValue(front,"fastBumpDamping"),.25f,2.1f),Mathf.Clamp(DraftValue(front,"fastReboundDamping"),.375f,2.6f));
  if(kit)coefficient*=LongTravelCore.DampingGain(DraftTravel(front),graphStroke*DraftTravel(front),velocity,DraftValue(front,"bypassDamping"),DraftValue(front,"droopDamping"));return (float)(coefficient*speed*1000);
 }
 void DrawDampingGraphs(){
  bool front=graphAxle==0,kit=DraftValue(front,"suspensionKit")>0;float travel=DraftTravel(front),sag=(float)LongTravelCore.StaticDepth(DraftLoad(front),DraftValue(front,"springRateNmm"),travel);
  GUI.Label(new Rect(1094,191,308,28),"DAMPER FORCE / ONE WHEEL",sectionStyle);GUI.Label(new Rect(1094,221,308,33),"Higher force = more resistance to wheel movement.",small);
  GUI.Label(new Rect(1094,261,210,23),kit?"Compressed: "+(graphStroke*100).ToString("0")+"% of travel":"Standard: no position gain",small);
  bool old=GUI.enabled;GUI.enabled=old&&kit;if(Button(new Rect(1313,257,89,28),"AT REST",false))graphStroke=Mathf.Clamp01(sag/travel);graphStroke=GUI.HorizontalSlider(new Rect(1094,293,308,13),graphStroke,0,1);GUI.enabled=old;
  GUI.Label(new Rect(1094,316,308,23),"Test wheel speed: "+(graphDamperSpeed*1000).ToString("0")+" mm/s",small);graphDamperSpeed=GUI.HorizontalSlider(new Rect(1094,345,308,13),graphDamperSpeed,0,1);
  float max=100;for(int curve=0;curve<2;curve++)for(int i=0;i<=80;i++)max=Mathf.Max(max,DampingForcePreview(front,curve==0,i/80f));max=Mathf.Ceil(max*1.1f/100)*100;
  for(int curve=0;curve<2;curve++){
   bool compression=curve==0;Color color=compression?Accent:Orange;Rect r=new Rect(1094,curve==0?398:603,308,105);
   DampingAxes(r,max,compression?"Push up / COMPRESSION (N)":"Return down / REBOUND (N)");Vector2 previous=Vector2.zero;
   for(int i=0;i<=80;i++){float speed=i/80f;Vector2 at=new Vector2(r.x+speed*r.width,r.yMax-DampingForcePreview(front,compression,speed)/max*r.height);if(i>0)Line(previous,at,color,2);previous=at;}
   float force=DampingForcePreview(front,compression,graphDamperSpeed);Vector2 dot=new Vector2(r.x+graphDamperSpeed*r.width,r.yMax-force/max*r.height);Line(new Vector2(dot.x,r.y),new Vector2(dot.x,r.yMax),new Color(.55f,.6f,.65f,.5f),1);Box(new Rect(dot.x-3,dot.y-3,6,6),color);
   var original=GUI.color;GUI.color=color;GUI.Label(new Rect(1094,r.yMax+31,308,23),"At "+(graphDamperSpeed*1000).ToString("0")+" mm/s: "+force.ToString("0")+" N",small);GUI.color=original;
  }
  GUI.Label(new Rect(1094,774,308,44),"Wheel speed is suspension movement, not road speed. Model forces, not a measured damper dyno.",small);
 }
 void DampingAxes(Rect r,float max,string title){
  GUI.Label(new Rect(r.x,r.y-29,r.width,25),title,label);Box(r,new Color(.06f,.08f,.10f));
  for(int i=0;i<=4;i++){float y=r.yMax-r.height*i/4;Line(new Vector2(r.x,y),new Vector2(r.xMax,y),new Color(.18f,.23f,.27f),1);if(i%2==0)GUI.Label(new Rect(r.x+3,Mathf.Clamp(y-19,r.y+2,r.yMax-20),75,20),(max*i/4).ToString("0",CultureInfo.InvariantCulture),small);}
  GUI.Label(new Rect(r.x,r.yMax+4,r.width,23),"0         Wheel movement speed        1000 mm/s",small);
 }
 void OnDestroy(){destroying=true;Close();if(pixel!=null)Destroy(pixel);if(sparky!=null)Destroy(sparky);if(Instance==this)Instance=null;}
}
