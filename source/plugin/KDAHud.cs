using UnityEngine;

public class KDAHud : PartModule {
 [KSPField(isPersistant=true)] public bool showHud=true;
 [KSPField(isPersistant=true)] public float hudX=-1,hudY=-1;
 [KSPField(isPersistant=true)] public float cameraSide,cameraForward,cameraHeight;
 bool cameraSettings;
 public bool CameraSettingsOpen {get{return cameraSettings;}}
 public Vector3 CameraPositionBody {get{return new Vector3(cameraSide,cameraForward,-cameraHeight);}}
 public void ToggleCameraSettings(){cameraSettings=!cameraSettings;}
 static float CameraValue(float value,float limit){return float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Round(Mathf.Clamp(value,-limit,limit)*100)/100;}
 public void SetCameraPosition(float side,float forward,float height){cameraSide=CameraValue(side,.3f);cameraForward=CameraValue(forward,.6f);cameraHeight=CameraValue(height,.35f);}
 KDAEngine engine;Rect window;GUIStyle label,small,large,title,gear,button,brand;Texture2D pixel;bool locked;
 readonly System.Collections.Generic.List<Texture2D> styleTextures=new System.Collections.Generic.List<Texture2D>();
 static readonly Color Accent=new Color(.20f,.89f,.75f,1);
 string LockName="KDAHUD";
 public int DrawFrames {get;private set;}
 KDATelemetry telemetry;
 public override void OnStart(StartState s){base.OnStart(s);SetCameraPosition(cameraSide,cameraForward,cameraHeight);LockName="KDAHUD"+part.GetInstanceID();engine=part.FindModuleImplementing<KDAEngine>();window=new Rect(hudX<0?Screen.width-465:hudX,hudY<0?Screen.height-420:hudY,445,385);if(HighLogic.LoadedSceneIsFlight){telemetry=part.gameObject.GetComponent<KDATelemetry>();if(telemetry==null)telemetry=part.gameObject.AddComponent<KDATelemetry>();telemetry.Body=part;var camera=part.gameObject.GetComponent<KDADriverCamera>();if(camera==null)camera=part.gameObject.AddComponent<KDADriverCamera>();camera.Hud=this;}}
 public void SetHudVisible(bool visible){showHud=visible;Unlock();}
 [KSPEvent(guiActive=true,guiName="Show/hide driving HUD",active=true)] public void ToggleHud(){SetHudVisible(!showHud);}
 [KSPAction("Show/hide driving HUD")] public void ToggleHudAction(KSPActionParam p){ToggleHud();}
 [KSPEvent(guiActive=true,guiName="Reset HUD position",active=true)] public void ResetHud(){hudX=hudY=-1;window.x=Screen.width-465;window.y=Screen.height-380;}
 void Styles(){
  pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();
  label=new GUIStyle(GUI.skin.label){fontSize=14,alignment=TextAnchor.MiddleCenter};label.normal.textColor=Color.white;
  small=new GUIStyle(label){fontSize=11};small.normal.textColor=new Color(.67f,.76f,.79f);large=new GUIStyle(label){fontSize=46,fontStyle=FontStyle.Bold};title=new GUIStyle(label){fontSize=12,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip};gear=new GUIStyle(large){fontSize=29};
  brand=new GUIStyle(title){fontSize=15,fontStyle=FontStyle.Bold};brand.normal.textColor=Accent;
  button=new GUIStyle(GUI.skin.button){fontSize=11,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,border=new RectOffset(1,1,1,1),padding=new RectOffset(3,3,2,2)};
  button.normal.background=Surface(new Color(.075f,.115f,.13f),new Color(.18f,.27f,.29f));button.normal.textColor=new Color(.85f,.92f,.94f);
  button.hover.background=Surface(new Color(.10f,.23f,.23f),Accent);button.hover.textColor=Color.white;
  button.active.background=Surface(new Color(.10f,.37f,.32f),Accent);button.active.textColor=Color.white;
  button.focused.background=button.hover.background;button.focused.textColor=Color.white;
 }
 Texture2D Surface(Color fill,Color edge){var t=new Texture2D(3,3,TextureFormat.RGBA32,false);for(int y=0;y<3;y++)for(int x=0;x<3;x++)t.SetPixel(x,y,x==1&&y==1?fill:edge);t.filterMode=FilterMode.Point;t.Apply();styleTextures.Add(t);return t;}
 bool Button(Rect r,string text){return Button(r,new GUIContent(text));}
 bool Button(Rect r,GUIContent content){return GUI.Button(r,content,button);}
 bool Visible(){return HighLogic.LoadedSceneIsFlight&&part!=null&&engine!=null&&vessel!=null&&vessel==FlightGlobals.ActiveVessel&&!vessel.packed&&showHud&&!MapView.MapIsEnabled&&(KSP.UI.UIMasterController.Instance==null||KSP.UI.UIMasterController.Instance.IsUIShowing);}
 void OnGUI(){
  if(!Visible()){Unlock();return;}if(pixel==null)Styles();
  window.x=Mathf.Clamp(window.x,0,Mathf.Max(0,Screen.width-window.width));window.y=Mathf.Clamp(window.y,0,Mathf.Max(0,Screen.height-window.height));
  bool hover=Input.GetMouseButton(0)&&window.Contains(new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y));
  if(hover&&!locked){InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS&~ControlTypes.WHEEL_THROTTLE,LockName);locked=true;}else if(!hover)Unlock();
  window=GUI.Window(part.GetInstanceID()+0x4BDA,window,DrawWindow,"",GUIStyle.none);hudX=window.x;hudY=window.y;
 }
 void DrawWindow(int id){
  if(Event.current.type==EventType.Repaint)DrawFrames++;
  var kda=part.FindModuleImplementing<KDAController>();
  float extra=kda!=null&&kda.driveMode==3?32:0;
  var lights=part.FindModuleImplementing<KDALighting>();
  float baseHeight=385+extra+(lights!=null&&lights.Supported?32:0);
  float nitrousRow=baseHeight-8;if(engine.nitrousKit==1)baseHeight+=36;
  window.height=baseHeight+32+(cameraSettings?154:0);
  Box(new Rect(0,0,445,window.height),new Color(.018f,.032f,.042f,.98f));Box(new Rect(0,0,445,3),Accent);
  GUI.Label(new Rect(14,6,280,24),"KDA  /  DRIVE",brand);GUI.Label(new Rect(310,7,121,23),"LIVE TELEMETRY",small);
  Box(new Rect(12,31,421,1),new Color(.13f,.24f,.26f));
  GUI.BeginGroup(new Rect(0,32,445,window.height-32));
  Box(new Rect(12,80,142,145),new Color(.035f,.065f,.077f));Box(new Rect(291,80,142,145),new Color(.035f,.065f,.077f));
  GUI.Label(new Rect(14,8,325,23),(part.partInfo==null?"KDA Vehicle":part.partInfo.title)+"  /  "+engine.transmissionDisplay,title);
  if(Button(new Rect(410,8,23,23),new GUIContent("×","Hide HUD / reopen with the KDA toolbar button")))SetHudVisible(false);
  if(Button(new Rect(380,8,24,23),new GUIContent("","Driver camera settings")))ToggleCameraSettings();
  DrawSettingsGear(new Vector2(392,19));
  if(Button(new Rect(14,36,130,25),engine.engineRunning?"ENGINE: STOP":"ENGINE: START")){
   if(engine.engineRunning)engine.StopEngine();else engine.StartEngine();
  }
  var controls=part.FindModuleImplementing<KDACarControls>();
  if(controls!=null){if(Button(new Rect(155,36,66,25),"TCS ON")&&!controls.DriftLocked)controls.EnableTcs();if(Button(new Rect(225,36,66,25),"TCS OFF"))controls.DisableTcs();GUI.Label(new Rect(155,61,136,17),controls.DriftLocked?"TCS LOCKED / DRIFT":controls.TcsActive?"TCS ENABLED":"TCS DISABLED",small);}
  var recovery=part.FindModuleImplementing<KDAProtection>();
  if(recovery!=null){bool wasEnabled=GUI.enabled;GUI.enabled=wasEnabled&&recovery.CanFlip;
   if(Button(new Rect(310,36,123,25),new GUIContent("FLIP UPRIGHT","Upside down, landed, and settled below 1.8 km/h")))recovery.FlipUpright();GUI.enabled=wasEnabled;}
  Gauge(new Vector2(89,127),65,engine.engineRpm,Mathf.Max(9000,engine.revLimitRpm),"RPM ×1000",true);
  Gauge(new Vector2(357,127),65,engine.boostBar,Mathf.Max(1.2f,engine.HudMaximumBoost),"BOOST / BAR",false);
  GUI.Label(new Rect(160,75,125,55),engine.speedKmh.ToString("F0"),large);GUI.Label(new Rect(160,128,125,20),"km/h",label);
  Box(new Rect(192,158,60,43),new Color(.06f,.20f,.19f));Box(new Rect(192,200,60,2),Accent);GUI.Label(new Rect(192,158,60,43),engine.gearDisplay,gear);
  GUI.Label(new Rect(20,194,140,23),engine.engineRpm.ToString("F0")+" rpm",label);GUI.Label(new Rect(287,194,140,23),engine.boostBar.ToString("F2")+" bar",label);
  float fuel=0;var resource=part.Resources["LiquidFuel"];if(resource!=null&&resource.maxAmount>0)fuel=(float)(resource.amount/resource.maxAmount);
  Box(new Rect(16,232,413,3),new Color(.18f,.22f,.26f));Box(new Rect(16,232,413*Mathf.Clamp01(fuel),3),new Color(.25f,.75f,.58f));
  string status=engine.BurnoutActive?"BURNOUT  /  RELEASE W OR S TO EXIT":engine.LimiterEngaged?"REV LIMITER":engine.status+"   /   FUEL "+(fuel*100).ToString("F0")+"%";
  var old=GUI.color;GUI.color=engine.BurnoutActive||engine.LimiterEngaged?new Color(1,.68f,.22f):Color.white;GUI.Label(new Rect(12,235,421,19),status,small);GUI.color=old;
  if(kda!=null){
   GUI.Label(new Rect(12,258,104,23),"SC / "+kda.ModeName,small);
   for(int i=0;i<4;i++){GUI.color=i==kda.driveMode?new Color(.35f,1,.85f):Color.white;if(Button(new Rect(120+i*76,258,72,23),i==0?"ROAD":i==1?"SPORT":i==2?"RACE":"DRIFT"))kda.SelectDriveMode(i);}GUI.color=old;
   if(extra>0){
    GUI.Label(new Rect(12,285,150,23),"SC lock: "+kda.driftSteeringLock.ToString("F0")+"° (+15° keys)",small);
    kda.SetDriftSteeringLock(Mathf.Round(GUI.HorizontalSlider(new Rect(166,293,205,20),kda.driftSteeringLock,25,75)));
    if(Button(new Rect(379,285,25,23),"−"))kda.SetDriftSteeringLock(kda.driftSteeringLock-5);
    if(Button(new Rect(408,285,25,23),"+"))kda.SetDriftSteeringLock(kda.driftSteeringLock+5);
   }
   float displayedPower=kda.ThrottleScale;
   if(RoadSportCore.Applies(kda.driveMode))displayedPower*=(float)RoadSportCore.PowerCap(kda.driveMode,kda.stabilityEnabled&&kda.assistance>0,controls!=null&&controls.TcsActive);
   GUI.Label(new Rect(12,285+extra,421,19),kda.status+" / power "+(displayedPower*100).ToString("F0")+"%",small);
   if(controls!=null){
    if(Button(new Rect(12,312+extra,104,25),controls.absEnabled?"ABS: ON":"ABS: OFF"))controls.absEnabled=!controls.absEnabled;
    if(Button(new Rect(120,312+extra,104,25),kda.stabilityEnabled?"SC: ON":"SC: OFF"))kda.stabilityEnabled=!kda.stabilityEnabled;
    if(Button(new Rect(228,312+extra,98,25),"ALL OFF"))controls.DisableAll();
    if(Button(new Rect(330,312+extra,103,25),"ALL ON"))controls.EnableAll();
   }
  }
  if(telemetry!=null){
   if(Button(new Rect(12,344+extra,124,25),telemetry.Recording?"STOP RECORDING":"RECORD")){if(telemetry.Recording)telemetry.Stop("HUD stop");else telemetry.Begin();}
   GUI.Label(new Rect(140,344+extra,208,25),telemetry.Recording?"REC / "+telemetry.Samples+" samples":string.IsNullOrEmpty(telemetry.Status)?"Driving telemetry":telemetry.Status,small);
   bool enabled=GUI.enabled;GUI.enabled=telemetry.Recording;if(Button(new Rect(356,344+extra,77,25),"MARK"))telemetry.Mark();GUI.enabled=enabled;
  }
  if(lights!=null&&lights.Supported){
   float row=376+extra;
   if(Button(new Rect(12,row,116,25),lights.HeadlightsOn?"LIGHTS: ON":"LIGHTS: OFF"))lights.ToggleHeadlights();
   if(Button(new Rect(132,row,86,25),lights.highBeams?"HIGH BEAM":"LOW BEAM"))lights.highBeams=!lights.highBeams;
   if(Button(new Rect(222,row,48,25),lights.indicators==1?"< ON":"<"))lights.Left();
   if(Button(new Rect(274,row,48,25),lights.indicators==2?"ON >":">"))lights.Right();
   if(Button(new Rect(326,row,107,25),lights.indicators==3?"HAZARDS: ON":"HAZARDS"))lights.Hazard();
  }
  if(engine.nitrousKit==1){
   Color nos=engine.NitrousActive?new Color(.25f,.65f,1):Accent;
   GUI.Label(new Rect(12,nitrousRow,421,20),"NITROUS  "+(engine.NitrousActive?"SPRAYING":engine.NitrousDriftRecharge?"DRIFT RECHARGE x3":"HOLD "+((KeyCode)engine.nitrousKey).ToString())+"  /  "+(Mathf.Clamp01(engine.nitrousCharge)*100).ToString("F0")+"%",small);
   Box(new Rect(16,nitrousRow+24,413,4),new Color(.13f,.24f,.26f));Box(new Rect(16,nitrousRow+24,413*Mathf.Clamp01(engine.nitrousCharge),4),nos);
  }
  if(cameraSettings){
   float y=baseHeight-5;Box(new Rect(12,y,421,1),new Color(.2f,.55f,.59f));
   GUI.Label(new Rect(12,y+5,270,22),"DRIVER CAMERA POSITION",title);
   if(Button(new Rect(337,y+5,96,22),"RESET"))SetCameraPosition(0,0,0);
   cameraSide=CameraSlider(y+31,"Left / right",cameraSide,.3f);
   cameraForward=CameraSlider(y+62,"Back / forward",cameraForward,.6f);
   cameraHeight=CameraSlider(y+93,"Lower / higher",cameraHeight,.35f);
   SetCameraPosition(cameraSide,cameraForward,cameraHeight);
   GUI.Label(new Rect(12,y+122,421,22),"Live in driver view / saved with this car",small);
  }
  GUI.EndGroup();GUI.DragWindow(new Rect(0,0,375,67));
 }
 float CameraSlider(float y,string caption,float value,float limit){
  GUI.Label(new Rect(12,y,155,22),caption+" "+(value*100).ToString("+0;-0;0")+" cm",small);
  if(Button(new Rect(171,y,24,22),"−"))value-=.01f;
  value=GUI.HorizontalSlider(new Rect(202,y+8,195,18),value,-limit,limit);
  if(Button(new Rect(405,y,28,22),"+"))value+=.01f;
  return CameraValue(value,limit);
 }
 void DrawSettingsGear(Vector2 center){for(int i=0;i<8;i++){float angle=i*45;Line(Point(center,7,angle),Point(center,10,angle),Color.white,3);}for(int i=0;i<16;i++)Line(Point(center,6,i*22.5f),Point(center,6,(i+1)*22.5f),Color.white,2);Box(new Rect(center.x-2,center.y-2,4,4),new Color(.1f,.14f,.17f));}
 void Gauge(Vector2 center,float radius,float value,float max,string caption,bool rpm){
  for(int i=0;i<52;i++){float a=Mathf.Lerp(-130,130,i/51f);bool red=rpm&&i/51f*max>=engine.revLimitRpm*.92f;bool lit=i/51f<=Mathf.Clamp01(value/max);
   Line(Point(center,radius,a),Point(center,radius,a+3),red?new Color(1,.28f,.23f):lit?Accent:new Color(.13f,.25f,.28f),lit?4:2);
  }
  for(int i=0;i<=6;i++){float a=Mathf.Lerp(-130,130,i/6f);Line(Point(center,radius-3,a),Point(center,radius-11,a),Color.white,1);
   string text=(max*i/6/(rpm?1000:1)).ToString(rpm?"F1":"F1");var p=Point(center,radius-23,a);GUI.Label(new Rect(p.x-15,p.y-9,30,18),text,small);
  }
  Line(center,Point(center,radius-15,Mathf.Lerp(-130,130,Mathf.Clamp01(value/max))),new Color(1,.67f,.28f),2);
  Box(new Rect(center.x-3,center.y-3,6,6),Color.white);GUI.Label(new Rect(center.x-67,center.y+42,134,19),caption,small);
 }
 Vector2 Point(Vector2 center,float radius,float angle){float a=angle*Mathf.Deg2Rad;return center+new Vector2(Mathf.Sin(a),-Mathf.Cos(a))*radius;}
 void Box(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,pixel);GUI.color=old;}
 void Line(Vector2 a,Vector2 b,Color c,float width){var matrix=GUI.matrix;var old=GUI.color;GUI.color=c;GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);GUI.DrawTexture(new Rect(a.x,a.y-width/2,(b-a).magnitude,width),pixel);GUI.matrix=matrix;GUI.color=old;}
 void Unlock(){if(locked){InputLockManager.RemoveControlLock(LockName);locked=false;}}
 void OnDisable(){Unlock();}void OnDestroy(){Unlock();if(pixel!=null)Destroy(pixel);foreach(var texture in styleTextures)if(texture!=null)Destroy(texture);styleTextures.Clear();}
}

// One launcher entry per flight scene, independent of a part's hidden HUD.
[KSPAddon(KSPAddon.Startup.Flight,false)]
public sealed class KDAHudLauncher : MonoBehaviour {
 KSP.UI.Screens.ApplicationLauncherButton launcherButton;
 KSP.UI.Screens.ApplicationLauncher owner;
 Texture2D icon;bool lastShown;
 void Start(){GameEvents.onGUIApplicationLauncherReady.Add(AddButton);AddButton();}
 void AddButton(){
  var launcher=KSP.UI.Screens.ApplicationLauncher.Instance;
  if(launcher==null||!KSP.UI.Screens.ApplicationLauncher.Ready||launcherButton!=null)return;
  if(icon==null)icon=CreateIcon();
  owner=launcher;
  launcherButton=launcher.AddModApplication(Show,Hide,null,null,null,null,KSP.UI.Screens.ApplicationLauncher.AppScenes.FLIGHT,icon);
  var hud=ActiveHud();lastShown=hud!=null&&hud.showHud;
  if(lastShown)launcherButton.SetTrue(false);else launcherButton.SetFalse(false);
 }
 static KDAHud ActiveHud(){var v=FlightGlobals.ActiveVessel;if(v==null)return null;foreach(var p in v.parts){var hud=p.FindModuleImplementing<KDAHud>();if(hud!=null)return hud;}return null;}
 static void SetVisible(bool visible){var v=FlightGlobals.ActiveVessel;if(v==null)return;foreach(var p in v.parts){var hud=p.FindModuleImplementing<KDAHud>();if(hud!=null)hud.SetHudVisible(visible);}}
 void Show(){SetVisible(true);lastShown=true;}
 void Hide(){SetVisible(false);lastShown=false;}
 void Update(){
  if(launcherButton==null)AddButton();if(launcherButton==null)return;
  var hud=ActiveHud();if(hud==null){launcherButton.Disable(false);lastShown=false;launcherButton.SetFalse(false);return;}
  launcherButton.Enable(false);if(hud.showHud==lastShown)return;
  lastShown=hud.showHud;if(lastShown)launcherButton.SetTrue(false);else launcherButton.SetFalse(false);
 }
 internal static Texture2D CreateIcon(){
  // A legible KDA badge at the stock toolbar's small native size.
  var t=new Texture2D(38,38,TextureFormat.RGBA32,false);t.name="KDA driving HUD";t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Clamp;
  for(int y=0;y<38;y++)for(int x=0;x<38;x++)t.SetPixel(x,y,y<3||y>34?new Color(.2f,.89f,.75f):new Color(.025f,.055f,.065f));
  // K / D / A: five rows, three columns each with a blank column between.
  string[] letters={"101110100110101","110101101101110","010101111101101"};
  for(int n=0;n<3;n++)for(int y=0;y<5;y++)for(int x=0;x<3;x++)if(letters[n][y*3+x]=='1')for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)t.SetPixel(8+n*8+x*2+dx,14+(4-y)*2+dy,new Color(.7f,1,.92f));
  t.Apply();return t;
 }
 void OnDestroy(){GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);if(launcherButton!=null&&owner!=null)owner.RemoveModApplication(launcherButton);launcherButton=null;if(icon!=null)Destroy(icon);}
}
