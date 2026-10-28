using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI;
using KSP.UI.Screens;

// Presentation and editor integration only. Showroom rotation moves the camera,
// never craft transforms. Native placement and part eligibility remain in charge.
public sealed partial class KDAGarage {
 public enum GarageView {Cars,Build,Tune}
 sealed class CarEntry {public string Path,Name,Source,BodyPart;public int Parts;public Texture2D Preview;public bool PreviewFailed;}
 sealed class CanvasState {public Canvas Canvas;public bool Enabled;}
 readonly List<CanvasState> hiddenCanvas=new List<CanvasState>();
 readonly List<CarEntry> cars=new List<CarEntry>();
 readonly List<AvailablePart> palette=new List<AvailablePart>();
 EditorPartListFilter<AvailablePart> garageFilter;
 GarageView view=GarageView.Cars;Vector2 carScroll,partScroll;
 string search="",partSearch="",saveName="",error="";int partGroup,selectedCar=-1;
 bool spinning=true,showGraphs,saveDialog,overwrite,editingText;
 CarEntry loadRequest;
 Camera editorCamera;Rect originalViewport;Matrix4x4 originalProjection;float originalAspect;
 CameraClearFlags originalClearFlags;Color originalBackground;
 SPHCamera sph;VABCamera vab;Vector3 savedPivot,savedOffset;float savedDistance,savedPitch,savedHeading,savedMinDistance,savedMinHeight,savedMinPitch,savedMaxPitch;
 public enum TuningCamera {ThreeQuarter,Left,Right,Front,Rear,Top}
 int tuningCamera;
 public int CurrentTuningCamera {get{return tuningCamera;}}
 static readonly Color PanelColor=new Color(.025f,.035f,.045f,1),Muted=new Color(.58f,.65f,.69f,1);
 public GarageView CurrentView {get{return view;}}
 public int CarCount {get{return cars.Count;}}
 public int PaletteCount {get{return palette.Count;}}
 public bool ShowroomSpinning {get{return spinning;}}
 public Vector3 FramingCenter {get;private set;}
 public bool StockUIHidden {get{foreach(var c in hiddenCanvas)if(c.Canvas!=null&&c.Canvas.enabled)return false;return hiddenCanvas.Count>0;}}
 public bool ModalOpen {get{return prebuildDialog||saveDialog||loadRequest!=null||deleteRequest!=null;}}
 public bool HasLoosePart {get{var selected=EditorLogic.SelectedPart;return selected!=null&&(ship==null||!ship.parts.Contains(selected));}}
 public Rect CarRect {get{return view==GarageView.Cars?new Rect(0,100,1080,510):new Rect(view==GarageView.Tune?442:330,100,(view==GarageView.Tune&&showGraphs?1076:1440)-(view==GarageView.Tune?442:330),730);}}
 Vector2 GarageMouse(){return new Vector2(Input.mousePosition.x*1440f/Screen.width,(Screen.height-Input.mousePosition.y)*900f/Screen.height);}
 void HideCanvas(Canvas canvas){if(canvas==null)return;hiddenCanvas.Add(new CanvasState{Canvas=canvas,Enabled=canvas.enabled});canvas.enabled=false;}
 void OpenEditor(){
  view=GarageView.Cars;spinning=true;showGraphs=false;tuningCamera=0;CancelModal();error="";
  var ui=UIMasterController.Instance;if(ui!=null){HideCanvas(ui.mainCanvas);HideCanvas(ui.appCanvas);}
  var list=EditorPartList.Instance;if(list!=null){garageFilter=new EditorPartListFilter<AvailablePart>("KDA.Garage.Parts",IsKDAPart,"KDA garage parts only");list.ExcludeFilters.AddFilter(garageFilter);list.Refresh();}
  editorCamera=EditorLogic.fetch.editorCamera;if(editorCamera!=null){originalViewport=editorCamera.rect;originalProjection=editorCamera.projectionMatrix;originalAspect=editorCamera.aspect;originalClearFlags=editorCamera.clearFlags;originalBackground=editorCamera.backgroundColor;}
  sph=EditorDriver.fetch==null||EditorDriver.editorFacility!=EditorFacility.SPH?null:EditorDriver.fetch.sphCamera;vab=EditorDriver.fetch==null||EditorDriver.editorFacility!=EditorFacility.VAB?null:EditorDriver.fetch.vabCamera;
  if(sph!=null){savedPivot=sph.pivotPosition;savedDistance=sph.Distance;savedPitch=sph.camPitch;savedHeading=sph.camHdg;savedOffset=sph.offset;savedMinDistance=sph.minDistance;savedMinHeight=sph.minHeight;savedMinPitch=sph.minPitch;savedMaxPitch=sph.maxPitch;sph.minDistance=1.2f;sph.minHeight=0;sph.maxPitch=90;}
  else if(vab!=null){savedPivot=vab.GetPivot().position;savedDistance=vab.Distance;savedPitch=vab.camPitch;savedHeading=vab.camHdg;savedMinDistance=vab.minDistance;savedMinHeight=vab.minHeight;savedMinPitch=vab.minPitch;savedMaxPitch=vab.maxPitch;vab.minDistance=1.2f;vab.minHeight=0;vab.maxPitch=90;}
  RefreshCars();RefreshPalette();FrameCar();SetCinematic(cinematic);MaintainGarageCanvases();UpdateEditor();UpdateLock();
 }
 void CloseEditor(){
  CancelModal();ReleaseGarageGrade();ReleaseGarageInput();editingText=false;InputLockManager.RemoveControlLock("KDA.Garage.Navigation");foreach(var state in hiddenCanvas)if(state.Canvas!=null)state.Canvas.enabled=state.Enabled;hiddenCanvas.Clear();
  if(garageFilter!=null&&EditorPartList.Instance!=null){EditorPartList.Instance.ExcludeFilters.RemoveFilter(garageFilter);EditorPartList.Instance.Refresh();}garageFilter=null;
  if(editorCamera!=null){editorCamera.rect=originalViewport;editorCamera.aspect=originalAspect;editorCamera.projectionMatrix=originalProjection;editorCamera.clearFlags=originalClearFlags;editorCamera.backgroundColor=originalBackground;}ClearPreviews();
  if(sph!=null){sph.PlaceCamera(savedPivot,savedDistance);sph.camPitch=savedPitch;sph.camHdg=savedHeading;sph.offset=savedOffset;sph.minDistance=savedMinDistance;sph.minHeight=savedMinHeight;sph.minPitch=savedMinPitch;sph.maxPitch=savedMaxPitch;}
  else if(vab!=null){vab.PlaceCamera(savedPivot,savedDistance);vab.camPitch=savedPitch;vab.camHdg=savedHeading;vab.minDistance=savedMinDistance;vab.minHeight=savedMinHeight;vab.minPitch=savedMinPitch;vab.maxPitch=savedMaxPitch;}
  sph=null;vab=null;editorCamera=null;
 }
 void UpdateNavigationLock(){
  ControlTypes mask=ControlTypes.EDITOR_SAVE|ControlTypes.EDITOR_LOAD|ControlTypes.EDITOR_NEW|ControlTypes.EDITOR_EXIT|ControlTypes.EDITOR_LAUNCH|ControlTypes.EDITOR_MODE_SWITCH|ControlTypes.EDITOR_TAB_SWITCH|ControlTypes.EDITOR_EDIT_NAME_FIELDS|ControlTypes.EDITOR_CREW|ControlTypes.EDITOR_ICON_PICK|ControlTypes.EDITOR_ICON_HOVER|ControlTypes.EDITOR_EDIT_STAGES|ControlTypes.APPLAUNCHER_BUTTONS;
  if(StockPointerBlocked(GarageMouse()))mask|=ControlTypes.EDITOR_PAD_PICK_COPY|ControlTypes.EDITOR_PAD_PICK_PLACE|ControlTypes.EDITOR_GIZMO_TOOLS|ControlTypes.EDITOR_ROOT_REFLOW|ControlTypes.TWEAKABLES_ANYCONTROL;
  if(editingText||ModalOpen||view!=GarageView.Build)mask|=ControlTypes.EDITOR_GIZMO_TOOLS|ControlTypes.EDITOR_PAD_PICK_COPY|ControlTypes.EDITOR_PAD_PICK_PLACE|ControlTypes.EDITOR_ROOT_REFLOW|ControlTypes.EDITOR_UNDO_REDO|ControlTypes.EDITOR_SYM_SNAP_UI;
  InputLockManager.SetControlLock(mask,"KDA.Garage.Navigation");
 }
 void UpdateEditor(){
  UpdateGarageProjection();UpdateCollectionPreview();
  if(view==GarageView.Tune&&!cameraLocked&&!ModalOpen&&Input.GetMouseButton(1)&&(Mathf.Abs(Input.GetAxis("Mouse X"))+Mathf.Abs(Input.GetAxis("Mouse Y"))>.01f))tuningCamera=-1;
  if(view==GarageView.Cars&&ship!=null&&ship.parts.Count>0&&spinning&&!ModalOpen&&!loadingCar&&!Input.GetMouseButton(1)&&!Input.GetMouseButton(2)){
   if(sph!=null)sph.camHdg+=Time.unscaledDeltaTime*9*Mathf.Deg2Rad;else if(vab!=null)vab.camHdg+=Time.unscaledDeltaTime*9*Mathf.Deg2Rad;
  }
 }
 void LateUpdate(){if(open){MaintainGarageCanvases();if(KDAWorldGarage.ActiveSession&&(ship==null||ship.parts.Count==0))FrameEmptyBay();UpdateGarageProjection();}}
 void UpdateGarageProjection(){
  if(editorCamera==null)return;
  if(KDAWorldGarage.ActiveSession){editorCamera.clearFlags=CameraClearFlags.SolidColor;editorCamera.backgroundColor=new Color(.035f,.045f,.05f,1);}
  // Keep all native/deferred post-processing at full resolution. Panels crop
  // the rendered view; changing its pixel rectangle would stretch their blits.
  editorCamera.rect=originalViewport;editorCamera.aspect=(float)Screen.width/Screen.height;
  Rect r=CarRect;var projection=Matrix4x4.Perspective(editorCamera.fieldOfView,editorCamera.aspect,editorCamera.nearClipPlane,editorCamera.farClipPlane);
  projection.m02=1-2*r.center.x/1440;projection.m12=2*r.center.y/900-1;editorCamera.projectionMatrix=projection;
 }
 public void SetGraphsVisible(bool enabled){showGraphs=enabled;UpdateGarageProjection();FrameCar();}
 public bool SelectBuildTool(ConstructionMode mode){
  if(!open||view!=GarageView.Build||ModalOpen||EditorLogic.fetch==null)return false;
  BeginBuildCommand();
  EditorLogic.fetch.toolsUI.SetMode(mode,true);return EditorLogic.fetch.EditorConstructionMode==mode;
 }
 void BeginBuildCommand(){editingText=false;if(Event.current!=null)GUI.FocusControl("");EditorLogic.fetch.Unlock(LockName);InputLockManager.RemoveControlLock("KDA.Garage.Navigation");}
 public void SelectView(GarageView next){
  if(!open||loadingCar||ModalOpen||HasLoosePart)return;
  if(view==GarageView.Build&&next!=GarageView.Build){SelectBuildTool(ConstructionMode.Place);if(UIPartActionController.Instance!=null)UIPartActionController.Instance.Deselect(true);}
  if(view==GarageView.Tune&&next!=GarageView.Tune){DiscardPreview();if(model!=null){model.localPosition=modelPosition;model.localRotation=modelRotation;}}
  if(next==GarageView.Tune&&view!=next)BindCraft();view=next;editingText=false;if(Event.current!=null)GUI.FocusControl("");
  if(next==GarageView.Build)RefreshPalette();UpdatePreview();UpdateEditor();FrameCar();UpdateLock();
 }
 public void SetShowroomSpin(bool enabled){spinning=enabled;}
 public bool SelectTuningCamera(TuningCamera next){
  if(!open||view!=GarageView.Tune||loadingCar||ModalOpen||body==null||(int)next<0||(int)next>5)return false;
  tuningCamera=(int)next;FrameCar();return true;
 }
 Vector3 TuningCameraOffset(){
  switch(tuningCamera){case 1:return -body.transform.right;case 2:return body.transform.right;case 3:return body.transform.up;case 4:return -body.transform.up;case 5:return -body.transform.forward;default:return (-body.transform.right+body.transform.up-body.transform.forward*.45f).normalized;}
 }
 void DrawTuningCameras(){
  if(view!=GarageView.Tune)return;Rect car=CarRect;float step=(car.width-40)/6;string[] names={"3/4","LEFT","RIGHT","FRONT","REAR","TOP"};
  for(int i=0;i<6;i++)if(Button(new Rect(car.x+20+i*step,car.y+60,step-5,30),names[i],tuningCamera==i))SelectTuningCamera((TuningCamera)i);
 }
 public void FrameCar(){
  if(editorCamera==null)return;if(ship==null||ship.parts.Count==0){if(KDAWorldGarage.ActiveSession)FrameEmptyBay();return;}Bounds bounds=new Bounds();bool found=false;
  foreach(var p in ship.parts)if(p!=null)foreach(var r in p.GetComponentsInChildren<Renderer>(false)){
   if(!r.enabled||r.gameObject.layer==16||(!(r is MeshRenderer)&&!(r is SkinnedMeshRenderer)))continue;
   if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);
  }
  if(!found)return;FramingCenter=bounds.center;float radius=Mathf.Max(1,new Vector2(bounds.extents.x,bounds.extents.z).magnitude);
  float halfFov=editorCamera.fieldOfView*.5f*Mathf.Deg2Rad;float aspect=(float)Screen.width/Screen.height;
  float projectedHeight=bounds.extents.y*Mathf.Cos(12*Mathf.Deg2Rad)+radius*Mathf.Sin(12*Mathf.Deg2Rad);
  float distance=Mathf.Max(radius*1.85f,Mathf.Max(radius/(Mathf.Tan(halfFov)*aspect*(CarRect.width/1440)*.82f),projectedHeight/(Mathf.Tan(halfFov)*(CarRect.height/900)*.78f)));
  float pitch=12*Mathf.Deg2Rad,headingAngle=135*Mathf.Deg2Rad;
  if(view==GarageView.Tune&&body!=null){
   pitch=sph!=null?sph.camPitch:vab!=null?vab.camPitch:pitch;headingAngle=sph!=null?sph.camHdg:vab!=null?vab.camHdg:headingAngle;
   if(tuningCamera>=0){Vector3 offset=TuningCameraOffset().normalized;pitch=Mathf.Asin(Mathf.Clamp(offset.y,-1,1));headingAngle=Mathf.Abs(offset.y)>.9999f?Mathf.Atan2(body.transform.up.x,body.transform.up.z):Mathf.Atan2(-offset.x,-offset.z);}
   // Fit the actual view-space bounds, including wheel droop and widened kits.
   // Retain the perspective/full-screen projection used by visual mods.
   Quaternion rotation=Quaternion.Euler(pitch*Mathf.Rad2Deg,headingAngle*Mathf.Rad2Deg,0);Quaternion inverse=Quaternion.Inverse(rotation);distance=1.2f;
   float tx=Mathf.Tan(halfFov)*aspect*(CarRect.width/1440)*.86f,ty=Mathf.Tan(halfFov)*(CarRect.height/900)*.60f;
   for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){Vector3 corner=inverse*Vector3.Scale(bounds.extents,new Vector3(x,y,z));distance=Mathf.Max(distance,Mathf.Max(Mathf.Abs(corner.x)/tx,Mathf.Abs(corner.y)/ty)-corner.z);}
  }
  if(sph!=null){sph.PlaceCamera(bounds.center,Mathf.Max(sph.minDistance,distance));sph.offset=Vector3.zero;sph.camPitch=pitch;sph.camHdg=headingAngle;}
  else if(vab!=null){vab.PlaceCamera(bounds.center,Mathf.Max(vab.minDistance,distance));vab.camPitch=pitch;vab.camHdg=headingAngle;}
 }
 void FrameEmptyBay(){
  var world=KDAWorldGarage.Instance;if(world==null||!KDAWorldGarage.ActiveSession)return;FramingCenter=world.WorkshopBayFocus;
  if(sph!=null){sph.PlaceCamera(FramingCenter,10);sph.offset=Vector3.zero;sph.camPitch=12*Mathf.Deg2Rad;sph.camHdg=135*Mathf.Deg2Rad;}
  else if(vab!=null){vab.PlaceCamera(FramingCenter,10);vab.camPitch=12*Mathf.Deg2Rad;vab.camHdg=135*Mathf.Deg2Rad;}
 }
 public static bool IsKDAPart(AvailablePart part){
  if(part==null||part.partPrefab==null||part.category==PartCategories.none)return false;
  // Deliberately use identity, not manufacturer/title substring matching.
  return part.partUrl!=null&&part.partUrl.StartsWith("KDAVehicles/",StringComparison.OrdinalIgnoreCase)
   ||part.partPrefab.Modules.Contains("KDAEngine")||part.partPrefab.Modules.Contains("KDASuspension")||part.partPrefab.Modules.Contains("KDARaceSeat")||part.partPrefab.Modules.Contains("KDADoorway");
 }
 void RefreshPalette(){palette.Clear();foreach(var part in PartLoader.LoadedPartsList)if(IsKDAPart(part))palette.Add(part);palette.Sort((a,b)=>String.Compare(a.title,b.title,StringComparison.OrdinalIgnoreCase));}
 public bool PlaceKDAPart(string name){
  if(!open||view!=GarageView.Build||ModalOpen)return false;
  var part=palette.Find(p=>p.name==name);if(part==null||!PartAvailable(part))return false;
  if(!SelectBuildTool(ConstructionMode.Place))return false;
  editingText=false;if(Event.current!=null)GUI.FocusControl("");UpdateNavigationLock();
  EditorLogic.fetch.Unlock(LockName);EditorLogic.fetch.OnPartListIconTap(part);return true;
 }
 bool PartAvailable(AvailablePart part){
  if(!IsKDAPart(part))return false;var list=EditorPartList.Instance;
  if(list!=null)foreach(var filter in list.GreyoutFilters)if(!filter.FilterCriteria(part))return false;
  if(list!=null)foreach(var filter in list.ExcludeFilters)if(!filter.FilterCriteria(part))return false;
  return true;
 }
 void RefreshCars(){
  ClearPreviews();cars.Clear();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  string saveRoot=Path.Combine(KSPUtil.ApplicationRootPath,"saves",HighLogic.SaveFolder,"Ships");
  ScanCars(Path.Combine(saveRoot,"SPH"),"Your save",seen);ScanCars(Path.Combine(saveRoot,"VAB"),"Your save",seen);
  ScanCars(Path.Combine(KSPUtil.ApplicationRootPath,"Ships","SPH"),"Included",seen);
  ScanCars(Path.Combine(KSPUtil.ApplicationRootPath,"Ships","VAB"),"Included",seen);
  cars.Sort((a,b)=>{int order=String.Compare(b.Source,a.Source,StringComparison.Ordinal);return order!=0?order:String.Compare(a.Name,b.Name,StringComparison.OrdinalIgnoreCase);});selectedCar=-1;
 }
 void ScanCars(string directory,string source,HashSet<string> seen){
  if(!Directory.Exists(directory))return;
  try{foreach(string file in Directory.GetFiles(directory,"*.craft",SearchOption.AllDirectories)){
   string full=Path.GetFullPath(file);if(!seen.Add(full))continue;
   try{var config=ConfigNode.Load(full);if(config==null)continue;var parts=config.GetNodes("PART");bool kda=false;
    foreach(var p in parts){string id=p.GetValue("part")??"";int suffix=id.LastIndexOf('_');string name=suffix>0?id.Substring(0,suffix):id;var available=PartLoader.getPartInfoByName(name);if(available!=null&&available.partPrefab!=null&&available.partPrefab.Modules.Contains("KDAEngine")){kda=true;break;}}
    if(kda){string bodyId=CraftBody(config);if(source=="Included"&&cars.Exists(c=>c.Source=="Your save"&&c.BodyPart==bodyId))continue;cars.Add(new CarEntry{Path=full,Name=config.GetValue("ship")??Path.GetFileNameWithoutExtension(file),Parts=parts.Length,Source=source,BodyPart=bodyId});}
   }catch(Exception ex){Debug.LogWarning("[KDA Garage] Skipping unreadable craft: "+Path.GetFileName(full)+" / "+ex.Message);}
  }}catch(Exception ex){error="Could not read car folder: "+ex.Message;}
 }
 public string CarName(int index){return index>=0&&index<cars.Count?cars[index].Name:"";}
 public bool RequestCar(int index){if(!open||loadingCar||ModalOpen||HasLoosePart||index<0||index>=cars.Count)return false;selectedCar=index;loadRequest=cars[index];return true;}
 public bool ConfirmCarLoad(){
  if(loadingCar||loadRequest==null||EditorLogic.fetch==null)return false;var entry=loadRequest;
  try{
   var config=ConfigNode.Load(entry.Path);string missing="";if(config==null||!ShipConstruction.AllPartsFound(config,ref missing)){error="This craft has missing parts. Install its KDA vehicle package first.";loadRequest=null;return false;}
   // Preserve even unsaved work before replacing the editor ship.
   if(ship!=null&&ship.parts.Count>0){var recovery=ship.SaveShip();string directory=Path.Combine(KSPUtil.ApplicationRootPath,"saves",HighLogic.SaveFolder,"KDA-Garage-Recovery");Directory.CreateDirectory(directory);if(!recovery.Save(Path.Combine(directory,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".craft")))throw new IOException("Could not save a recovery copy.");}
   DiscardPreview();if(model!=null){model.localPosition=modelPosition;model.localRotation=modelRotation;}loadingName=entry.Name;loadingCar=true;loadRequest=null;error="";status="Loading "+entry.Name+"...";GUIUtility.keyboardControl=0;carLoadRoutine=StartCoroutine(LoadCarInGarage(entry.Path));return true;
  }catch(Exception ex){error="Car was not loaded: "+ex.Message;loadRequest=null;return false;}
 }
 public void CancelModal(){prebuildDialog=false;loadRequest=null;deleteRequest=null;deleteFingerprint="";saveDialog=false;overwrite=false;}
 public bool SaveCar(string name,bool allowOverwrite){
  if(loadingCar||ship==null||ship.parts.Count==0||PendingChanges>0||!ValidInputs)return false;
  name=(name??"").Trim();if(name.Length==0||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name=="."||name==".."){error="Use a car name without filename symbols.";return false;}
  try{string directory=ShipConstruction.GetCurrentGameShipsPathFor(EditorDriver.editorFacility);Directory.CreateDirectory(directory);string path=CarSavePath(directory,name);
   if(File.Exists(path)&&!allowOverwrite&&!IsLoadedCarFile(path)){overwrite=true;return false;}
   var config=ship.SaveShip();config.SetValue("ship",name,true);if(!config.Save(path))throw new IOException("Craft file could not be written.");
   RememberCarPath(path);EditorLogic.fetch.shipNameField.text=name;ship.shipName=name;EditorLogic.fetch.SetBackup();MarkEditorSaved(path,name);saveDialog=false;overwrite=false;RefreshCars();status="Saved "+name+" to this save's "+EditorDriver.editorFacility+" craft folder.";error="";return true;
  }catch(Exception ex){error="Could not save car: "+ex.Message;return false;}
 }
 void DrawEditor(){
  if(Event.current.type==EventType.Repaint)DrawFrames++;
  Box(new Rect(0,0,1440,100),PanelColor);Box(new Rect(0,0,1440,3),Accent);
  DrawGarageBrand();
  bool enabled=GUI.enabled;GUI.enabled=enabled&&!loadingCar&&!ModalOpen&&!HasLoosePart;
  string[] tabs={"CARS","BUILD","TUNE"};for(int i=0;i<3;i++)if(Button(new Rect(540+i*158,24,148,48),tabs[i],view==(GarageView)i))SelectView((GarageView)i);
  if(KDAWorldGarage.ActiveSession&&Button(new Rect(1044,27,174,42),EditorDriver.editorFacility==EditorFacility.SPH?"RETURN TO SPH":"RETURN TO VAB",false)){KDAWorldGarage.Instance.ReturnToSPH();GUI.enabled=enabled;return;}
  if(Button(new Rect(1224,27,192,42),KDAWorldGarage.ActiveSession?"EXIT GARAGE":EditorDriver.editorFacility==EditorFacility.SPH?"RETURN TO SPH":"RETURN TO VAB",false)){if(KDAWorldGarage.ActiveSession)KDAWorldGarage.Instance.ExitEditor();else Close();GUI.enabled=enabled;return;}GUI.enabled=enabled&&!loadingCar;
  if(view!=GarageView.Cars)Box(new Rect(0,100,view==GarageView.Tune?442:330,730),PanelColor);
  GUI.enabled=enabled&&!loadingCar&&!ModalOpen;
  if(view==GarageView.Cars)DrawCars();else if(view==GarageView.Build)DrawBuild();else DrawTuning();
  Rect car=CarRect;
  if(!loadingCar&&view==GarageView.Cars&&(ship==null||ship.parts.Count==0))GUI.Label(new Rect(car.center.x-220,car.center.y-35,440,70),"GARAGE EMPTY\nChoose a car from your collection below.",label);
  if(Button(new Rect(car.x+18,car.y+16,118,32),"FRAME CAR",false))FrameCar();
  if(view==GarageView.Cars&&Button(new Rect(car.x+146,car.y+16,174,32),spinning?"PAUSE ROTATION":"ROTATE CAR",spinning))spinning=!spinning;
  if(view==GarageView.Tune&&Button(new Rect(car.x+146,car.y+16,174,32),showGraphs?"HIDE GRAPHS":"SHOW GRAPHS",showGraphs))SetGraphsVisible(!showGraphs);
  DrawGarageTools();
  DrawTuningCameras();
  GUI.Label(new Rect(car.x+20,car.yMax-51,car.width-40,32),view==GarageView.Cars?"SHOWROOM   /   Right-drag to orbit. Scroll to zoom.":view==GarageView.Build?"BUILD   /   Right-drag: orbit. Scroll: zoom. WASDQE: rotate part.  1 Place   2 Move   3 Rotate   4 Root":"SUSPENSION   /   Ride height previews live. Apply before saving.",small);
  DrawFooter();GUI.enabled=enabled;
  if(loadingCar)DrawLoadingCar();
  if(ModalOpen)DrawModal();
 }
 void DrawCars(){DrawCollection();}
 void DrawBuild(){
  GUI.Label(new Rect(24,115,282,34),"KDA PARTS",heading);GUI.SetNextControlName("KDA.Text.PartSearch");partSearch=GUI.TextField(new Rect(24,162,282,30),partSearch,field);
  string[] groups={"ALL","CARS","WHEELS","CREW"};int fontSize=button.fontSize;button.fontSize=12;for(int i=0;i<4;i++)if(Button(new Rect(20+i*74,207,69,33),groups[i],partGroup==i)){partGroup=i;partScroll=Vector2.zero;}button.fontSize=fontSize;
  var visible=palette.FindAll(p=>(p.title.IndexOf(partSearch,StringComparison.OrdinalIgnoreCase)>=0)&&(partGroup==0||partGroup==1&&p.partPrefab.Modules.Contains("KDAEngine")||partGroup==2&&p.partPrefab.Modules.Contains("KDASuspension")||partGroup==3&&(p.partPrefab.Modules.Contains("KDARaceSeat")||p.partPrefab.Modules.Contains("KDADoorway"))));
  partScroll=GUI.BeginScrollView(new Rect(12,256,308,486),partScroll,new Rect(0,0,280,Mathf.Max(480,visible.Count*86)));
  float y=0;foreach(var p in visible){bool old=GUI.enabled;GUI.enabled=old&&PartAvailable(p);if(Button(new Rect(8,y,264,74),p.title+"\n"+(p.partPrefab.Modules.Contains("KDAEngine")?"Vehicle body":p.partPrefab.Modules.Contains("KDASuspension")?"Wheel & suspension":"Crew equipment"),false))PlaceKDAPart(p.name);GUI.enabled=old;y+=86;}GUI.EndScrollView();
  GUI.Label(new Rect(24,751,282,60),"Place and attach parts normally. Right-click parts for their settings. Press Delete to remove a selected part.",small);
 }
 void DrawFooter(){
  Box(new Rect(0,830,1440,70),PanelColor);Box(new Rect(0,830,1440,1),new Color(.16f,.22f,.25f));
  string message=error.Length>0?error:!ValidInputs?(TravelSetupIssue.Length>0?TravelSetupIssue:"Enter a value within the slider limits."):PendingChanges>0?PendingChanges+" changes in preview":status;
  GUI.Label(new Rect(24,842,view==GarageView.Tune?700:980,42),message,small);
  if(view==GarageView.Tune){if(Button(new Rect(750,847,176,36),"DISCARD",false))DiscardPreview();bool old=GUI.enabled;GUI.enabled=old&&PendingChanges>0&&ValidInputs;if(Button(new Rect(936,847,176,36),"APPLY TO CAR",true))ApplyPreview();GUI.enabled=old;}
  bool enabled=GUI.enabled;GUI.enabled=enabled&&ship!=null&&ship.parts.Count>0&&PendingChanges==0&&ValidInputs&&!HasLoosePart;
  if(Button(new Rect(1124,847,140,36),"SAVE CAR",false)){saveName=SuggestedCarName();saveDialog=true;error="";}
  if(Button(new Rect(1276,847,140,36),KDAWorldGarage.ActiveSession?"DRIVE OUT":"CREW / LAUNCH",false)){if(KDAWorldGarage.ActiveSession)KDAWorldGarage.Instance.Launch();else{Close();EditorLogic.fetch.SelectPanelCrew();}}GUI.enabled=enabled;
 }
 void DrawModal(){
  Box(new Rect(0,0,1440,900),new Color(0,0,0,.68f));Box(new Rect(420,260,600,344),PanelColor);Box(new Rect(420,260,600,3),Accent);
  if(prebuildDialog){DrawPrebuild();return;}
  if(deleteRequest!=null){DrawDeleteConfirmation();return;}
  if(loadRequest!=null){GUI.Label(new Rect(450,285,540,40),"LOAD "+loadRequest.Name+"?",heading);GUI.Label(new Rect(450,341,540,103),"This replaces the car in the editor. A recovery copy of your current craft will be kept in this save's KDA-Garage-Recovery folder. Unapplied tuning previews are discarded.",label);if(Button(new Rect(450,529,245,44),"CANCEL",false))CancelModal();if(Button(new Rect(717,529,273,44),"LOAD CAR",true))ConfirmCarLoad();}
  else{GUI.Label(new Rect(450,285,540,40),"SAVE CAR",heading);GUI.Label(new Rect(450,337,540,32),"Car name",label);GUI.SetNextControlName("KDA.Text.SaveName");string next=GUI.TextField(new Rect(450,379,540,38),saveName,field);if(next!=saveName){saveName=next;overwrite=false;}GUI.Label(new Rect(450,433,540,68),error.Length>0?error:overwrite?"A craft with this name already exists. Confirm to replace it.":"Saves the complete craft and its applied tuning to this game's save.",label);if(Button(new Rect(450,529,245,44),"CANCEL",false))CancelModal();if(Button(new Rect(717,529,273,44),overwrite?"REPLACE SAVED CAR":"SAVE",true))SaveCar(saveName,overwrite);}
 }
}
