using System;
using System.Collections.Generic;
using System.Globalization;
using ModuleWheels;
using UnityEngine;
using UnityEngine.Rendering;

// Cosmetic cockpit only. Wheel physics and drive-mode tuning stay with their
// existing owners. Imported wheel geometry is rigged separately from the body.
public sealed class KDAInterior:PartModule {
 [KSPField(guiActive=true,guiActiveEditor=true,guiName="Cockpit display")] public string interiorStatus="Waiting for model";
 sealed class ScreenSurface {public MeshFilter Filter;public Mesh OriginalMesh;public MeshRenderer Renderer;public Material[] OriginalMaterials;public int Slot;}
 readonly List<ScreenSurface> screens=new List<ScreenSurface>();
 readonly List<Mesh> meshes=new List<Mesh>();readonly List<Material> materials=new List<Material>();
 readonly List<ModuleWheelBase> wheels=new List<ModuleWheelBase>();
 KDAEngine engine;KDAController controller;Transform steering,ivaRoot,ivaWheel;Part ivaSeat;
 Vector3 axis=Vector3.up;Quaternion neutral;float lockDegrees=450,wheelAngle,nextDraw,limiterUntil,buildAfter;
 Texture2D display;CockpitRaster raster;bool built;int mode=-1;string gear="N";float rpm,speed;bool limiterFlash;
 public bool Supported {get{return screens.Count>0||steering!=null;}}
 public int ScreenCount {get{return screens.Count;}}public bool HasSteering {get{return steering!=null;}}
 public bool IvaVisible {get{return ivaRoot!=null&&ivaRoot.gameObject.activeSelf;}}
 public float SteeringDegrees {get{return wheelAngle;}}public Texture2D DisplayTexture {get{return display;}}
 public int DrawCount {get;private set;}public bool LimiterFlash {get{return limiterFlash;}}
 public int CutoutDepthCorrections {get;private set;}
 public override void OnStart(StartState s){base.OnStart(s);engine=part.FindModuleImplementing<KDAEngine>();controller=part.FindModuleImplementing<KDAController>();buildAfter=Time.unscaledTime+.65f;}
 static Vector3 Vector(string value,Vector3 fallback){if(string.IsNullOrEmpty(value))return fallback;var s=value.Split(',');float x,y,z;if(s.Length!=3||!float.TryParse(s[0],NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!float.TryParse(s[1],NumberStyles.Float,CultureInfo.InvariantCulture,out y)||!float.TryParse(s[2],NumberStyles.Float,CultureInfo.InvariantCulture,out z))return fallback;var v=new Vector3(x,y,z);return float.IsNaN(v.sqrMagnitude)||float.IsInfinity(v.sqrMagnitude)?fallback:v;}
 static bool ScreenMaterial(Material material){return material!=null&&(material.name.IndexOf("abscreen_",StringComparison.OrdinalIgnoreCase)>=0||material.name.StartsWith("digidash_screen",StringComparison.OrdinalIgnoreCase));}
 // Keep the stock forward queue, and provide missing masked shadow/depth
 // geometry. Only Deferred needs the visible cutout moved into its G-buffer.
 public static int CorrectCutoutDepth(Part body){
  return KDAExteriorDepth.Ensure(body);
 }
 void Build(){
  CutoutDepthCorrections=CorrectCutoutDepth(part);
  built=true;ConfigNode config=null;if(part.partInfo!=null)foreach(var n in GameDatabase.Instance.GetConfigNodes("KDA_VEHICLE_INTERIOR"))if(n.GetValue("part")==part.partInfo.name){config=n;break;}
  if(config!=null){steering=part.FindModelTransform(config.GetValue("steeringRoot")??"");axis=Vector(config.GetValue("steeringAxis"),Vector3.up).normalized;if(axis.sqrMagnitude<.5f)axis=Vector3.up;float value;if(float.TryParse(config.GetValue("steeringLock"),NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!float.IsNaN(value)&&!float.IsInfinity(value))lockDegrees=Mathf.Clamp(value,90,900);if(steering!=null)neutral=steering.localRotation;}
  foreach(var filter in part.GetComponentsInChildren<MeshFilter>(true)){
   if(filter.GetComponentInParent<Part>()!=part||filter.sharedMesh==null)continue;var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null)continue;var original=renderer.sharedMaterials;
   for(int slot=0;slot<original.Length;slot++)if(ScreenMaterial(original[slot])){
    if(display==null){display=new Texture2D(512,256,TextureFormat.RGBA32,false);display.name="KDA live cockpit HUD";display.wrapMode=TextureWrapMode.Clamp;display.filterMode=FilterMode.Bilinear;raster=new CockpitRaster(512,256);}
    // The source screen UVs occupy a rotated patch in a shared atlas. Map its
    // actual flat surface to the full display without changing exterior UVs.
    if(original.Length!=1||filter.sharedMesh.vertexCount!=4){Debug.LogWarning("[KDAINTERIOR] Unsupported non-quad screen "+filter.name);continue;}
    var source=filter.sharedMesh;var v=source.vertices;var bodyFrame=part.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;Vector3 lo=new Vector3(float.PositiveInfinity,0,float.PositiveInfinity),hi=new Vector3(float.NegativeInfinity,0,float.NegativeInfinity);
    for(int i=0;i<v.Length;i++){var p=bodyFrame.MultiplyPoint3x4(v[i]);lo.x=Mathf.Min(lo.x,p.x);lo.z=Mathf.Min(lo.z,p.z);hi.x=Mathf.Max(hi.x,p.x);hi.z=Mathf.Max(hi.z,p.z);}
    if(hi.x-lo.x<.01f||hi.z-lo.z<.01f)continue;
    var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++){var p=bodyFrame.MultiplyPoint3x4(v[i]);uv[i]=new Vector2((p.x-lo.x)/(hi.x-lo.x),(hi.z-p.z)/(hi.z-lo.z));}
    var mesh=UnityEngine.Object.Instantiate(source);mesh.name="KDA full-screen UV";mesh.uv=uv;meshes.Add(mesh);
    // KSP strips Unity's generic Unlit/Texture shader from the player. Use a
    // stock emissive shader with black albedo so the display stays readable
    // at night without needing any extra shader mod or lighting camera.
    var shader=Shader.Find("KSP/Emissive/Diffuse");if(shader==null)throw new InvalidOperationException("Stock emissive display shader unavailable");var material=new Material(shader);material.name="KDA cockpit "+original[slot].name;material.mainTexture=display;material.SetColor("_Color",Color.black);material.SetTexture("_Emissive",display);material.SetColor("_EmissiveColor",Color.white);materials.Add(material);
    var replacement=(Material[])original.Clone();replacement[slot]=material;screens.Add(new ScreenSurface{Filter=filter,OriginalMesh=source,Renderer=renderer,OriginalMaterials=original,Slot=slot});filter.sharedMesh=mesh;renderer.sharedMaterials=replacement;
   }
  }
  interiorStatus=(steering==null?"No rigged steering wheel":"Animated steering wheel")+" / "+screens.Count+" live screens";Debug.Log("[KDAINTERIOR] "+(part.partInfo==null?part.name:part.partInfo.name)+": "+interiorStatus);
 }
 void FindWheels(){wheels.Clear();if(vessel==null)return;foreach(Part p in vessel.parts){if(p==part||part.transform.InverseTransformPoint(p.transform.position).y<=0)continue;var s=p.FindModuleImplementing<ModuleWheelSteering>();var b=p.FindModuleImplementing<ModuleWheelBase>();if(s!=null&&s.steeringEnabled&&b!=null)wheels.Add(b);}}
 float ActualSteering(){
  if(wheels.Count==0)FindWheels();float sum=0;int count=0;
  foreach(var b in wheels){if(b==null||b.Wheel==null||b.Wheel.currentState==null)continue;var w=b.Wheel;float a=w.currentState.steerAngle;
   // Mirrored wheel parts have opposite native servo signs. Convert the
   // physical wheel's forward vector into the body's horizontal frame.
   Vector3 forward=part.transform.InverseTransformDirection(w.wheelCollider.transform.TransformDirection(Quaternion.AngleAxis(a,Vector3.up)*Vector3.forward));
   // Stock part orientation varies; normalize using its unsteered forward.
   Vector3 baseForward=part.transform.InverseTransformDirection(w.wheelCollider.transform.forward);Vector3 up=part.transform.InverseTransformDirection(w.wheelCollider.transform.up);
   float orientation=Mathf.Sign(Vector3.Dot(Vector3.Cross(baseForward,forward),Vector3.back));if(Mathf.Abs(a)<.001f)orientation=0;
   if(Mathf.Abs(Vector3.Dot(up,Vector3.back))<.5f)continue;
   sum+=orientation*Mathf.Abs(a)/Mathf.Max(1,w.maxSteerAngle);count++;
  }
  return count>0?Mathf.Clamp(sum/count,-1,1):0;
 }
 void CreateIva(Part seat){
  if(ivaRoot!=null)Destroy(ivaRoot.gameObject);ivaRoot=new GameObject("KDA live cockpit IVA").transform;ivaRoot.gameObject.layer=16;ivaRoot.SetParent(seat.internalModel.transform,false);ivaSeat=seat;ivaWheel=null;
  if(steering!=null){ivaWheel=new GameObject("KDA IVA steering wheel").transform;ivaWheel.gameObject.layer=16;ivaWheel.SetParent(ivaRoot,false);ivaWheel.localPosition=part.transform.InverseTransformPoint(steering.position);ivaWheel.localRotation=Quaternion.Inverse(part.transform.rotation)*steering.rotation;foreach(var filter in steering.GetComponentsInChildren<MeshFilter>(true)){var renderer=filter.GetComponent<MeshRenderer>();if(renderer!=null)CopyVisual(filter,renderer,ivaWheel,steering.worldToLocalMatrix*filter.transform.localToWorldMatrix,false);}}
  foreach(var screen in screens)CopyVisual(screen.Filter,screen.Renderer,ivaRoot,part.transform.worldToLocalMatrix*screen.Filter.transform.localToWorldMatrix,true);
 }
 void CopyVisual(MeshFilter filter,MeshRenderer renderer,Transform parent,Matrix4x4 frame,bool raised){
  var go=new GameObject(raised?"KDA IVA live display":"KDA IVA steering skin");go.layer=16;go.transform.SetParent(parent,false);go.transform.localPosition=frame.GetColumn(3);go.transform.localRotation=frame.rotation;go.transform.localScale=frame.lossyScale;
  if(raised){var normals=filter.sharedMesh.normals;if(normals.Length>0)go.transform.localPosition+=frame.MultiplyVector(normals[0]).normalized*.001f;}
  go.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;var copy=go.AddComponent<MeshRenderer>();copy.sharedMaterials=renderer.sharedMaterials;copy.shadowCastingMode=ShadowCastingMode.Off;copy.receiveShadows=false;
 }
 void Iva(){
  var camera=CameraManager.Instance;var crew=camera==null?null:camera.IVACameraActiveKerbal;Part seat=crew==null?null:crew.InPart;
  bool visible=camera!=null&&camera.currentCameraMode==CameraManager.CameraMode.IVA&&seat!=null&&seat.vessel==vessel&&seat.internalModel!=null;
  if(visible&&(ivaRoot==null||ivaSeat!=seat))CreateIva(seat);if(ivaRoot==null)return;ivaRoot.gameObject.SetActive(visible);if(!visible)return;
  ivaRoot.position=InternalSpace.WorldToInternal(part.transform.position);ivaRoot.rotation=InternalSpace.WorldToInternal(part.transform.rotation);ivaRoot.localScale=part.transform.lossyScale;
  if(ivaWheel!=null)ivaWheel.localRotation=Quaternion.Inverse(part.transform.rotation)*steering.rotation;
 }
 void LateUpdate(){
  if(!HighLogic.LoadedSceneIsFlight&&!HighLogic.LoadedSceneIsEditor)return;if(!built&&Time.unscaledTime>=buildAfter)try{Build();}catch(Exception e){built=true;interiorStatus="Cockpit data unavailable";Debug.LogError("[KDAINTERIOR] "+e);}
  if(!built)return;bool flight=HighLogic.LoadedSceneIsFlight&&vessel!=null&&!vessel.packed;
  if(steering!=null){float target=flight?ActualSteering()*lockDegrees:0;wheelAngle=Mathf.MoveTowards(wheelAngle,target,1800*Time.deltaTime);steering.localRotation=neutral*Quaternion.AngleAxis(wheelAngle,axis);}
  if(HighLogic.LoadedSceneIsFlight)Iva();
  if(display==null||engine==null)return;if(flight&&engine.LimiterEngaged)limiterUntil=Time.unscaledTime+.2f;
  if(Time.unscaledTime<nextDraw)return;nextDraw=Time.unscaledTime+.08f;
  bool visible=HighLogic.LoadedSceneIsEditor||flight&&(vessel==FlightGlobals.ActiveVessel||FlightCamera.fetch!=null&&Vector3.Distance(part.transform.position,FlightCamera.fetch.transform.position)<35);if(!visible)return;
  bool powered=engine.engineRunning&&(part.Resources["ElectricCharge"]==null||part.Resources["ElectricCharge"].amount>.001);mode=controller==null?-1:controller.driveMode;gear=engine.gearDisplay;rpm=engine.engineRpm;speed=engine.speedKmh;limiterFlash=powered&&Time.unscaledTime<limiterUntil&&((int)(Time.unscaledTime*8)%2==0);
  raster.Draw(powered,rpm,engine.revLimitRpm,speed,gear,controller==null?"SC OFF":controller.ModeName.ToUpperInvariant(),limiterFlash);display.SetPixels32(raster.Pixels);display.Apply(false,false);DrawCount++;
 }
 void OnDestroy(){if(ivaRoot!=null)Destroy(ivaRoot.gameObject);foreach(var s in screens){if(s.Filter!=null)s.Filter.sharedMesh=s.OriginalMesh;if(s.Renderer!=null)s.Renderer.sharedMaterials=s.OriginalMaterials;}foreach(var m in meshes)if(m!=null)Destroy(m);foreach(var m in materials)if(m!=null)Destroy(m);if(display!=null)Destroy(display);if(steering!=null)steering.localRotation=neutral;}
}

// Small reusable bitmap: no camera, canvas, font atlas or per-frame UI objects.
internal sealed class CockpitRaster {
 public readonly Color32[] Pixels;readonly int width,height;
 static readonly Color32 Black=new Color32(6,9,13,255),White=new Color32(238,246,246,255),Teal=new Color32(51,204,194,255),Red=new Color32(255,32,32,255),Dim=new Color32(36,52,62,255);
 static readonly Dictionary<char,string> Font=new Dictionary<char,string>{
 {'0',"01110100011001110101110011000101110"},{'1',"00100011000010000100001000010001110"},{'2',"01110100010000100010001000100011111"},{'3',"11110000010000101110000010000111110"},{'4',"00010001100101010010111110001000010"},{'5',"11111100001000011110000010000111110"},{'6',"01110100001000011110100011000101110"},{'7',"11111000010001000100010000100001000"},{'8',"01110100011000101110100011000101110"},{'9',"01110100011000101111000010000101110"},
 {'A',"01110100011000111111100011000110001"},{'C',"01111100001000010000100001000001111"},{'D',"11110100011000110001100011000111110"},{'E',"11111100001000011110100001000011111"},{'F',"11111100001000011110100001000010000"},{'G',"01111100001000010111100011000101111"},{'H',"10001100011000111111100011000110001"},{'I',"01110001000010000100001000010001110"},{'K',"10001100101010011000101001001010001"},{'L',"10000100001000010000100001000011111"},{'M',"10001110111010110101100011000110001"},{'N',"10001110011010110011100011000110001"},{'O',"01110100011000110001100011000101110"},{'P',"11110100011000111110100001000010000"},{'R',"11110100011000111110101001001010001"},{'S',"01111100001000001110000010000111110"},{'T',"11111001000010000100001000010000100"},{'V',"10001100011000110001100010101000100"},{'/',"00001000100001000100010000100010000"},{'-',"00000000000000011111000000000000000"}
 };
 public CockpitRaster(int w,int h){width=w;height=h;Pixels=new Color32[w*h];}
 void Box(int x,int y,int w,int h,Color32 color){for(int j=Math.Max(0,y);j<Math.Min(height,y+h);j++)for(int i=Math.Max(0,x);i<Math.Min(width,x+w);i++)Pixels[(height-1-j)*width+i]=color;}
 void Text(int x,int y,string value,int scale,Color32 color){if(value==null)return;foreach(char raw in value){char c=char.ToUpperInvariant(raw);string glyph;if(Font.TryGetValue(c,out glyph))for(int row=0;row<7;row++)for(int col=0;col<5;col++)if(glyph[row*5+col]=='1')Box(x+col*scale,y+row*scale,scale,scale,color);x+=6*scale;}}
 public void Draw(bool on,float rpm,float limit,float speed,string gear,string mode,bool flash){
  for(int i=0;i<Pixels.Length;i++)Pixels[i]=Black;if(!on)return;Box(0,0,width,4,Teal);Text(18,18,"RPM",2,flash?Red:White);
  float fraction=Mathf.Clamp01(rpm/Mathf.Max(1,limit));for(int i=0;i<24;i++)Box(90+i*16,15,12,22,i/24f<fraction?(flash?Red:i>=21?Red:Teal):Dim);
  Text(20,60,Mathf.Max(0,Mathf.RoundToInt(speed)).ToString(CultureInfo.InvariantCulture),10,White);Text(25,139,"KM/H",3,Teal);Box(348,59,143,106,Dim);Text(366,82,gear,7,White);
  Text(20,185,Mathf.Max(0,Mathf.RoundToInt(rpm)).ToString(CultureInfo.InvariantCulture)+" RPM",3,flash?Red:White);Box(18,222,476,2,Dim);Text(20,233,mode,2,Teal);if(flash)Text(280,185,"LIMIT",3,Red);
 }
}
