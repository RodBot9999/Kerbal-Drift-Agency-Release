using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using KerbalDriftAgency;

// All light geometry and material bindings come from exported data. This module
// only renders light; it never changes controls, brakes, torque or vessel mass.
public sealed class KDALighting:PartModule {
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="High beams")] public bool highBeams;
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Light brightness")]
 [UI_FloatRange(minValue=.25f,maxValue=2,stepIncrement=.05f)] public float brightness=1;
 [KSPField(isPersistant=true)] public int indicators;
 [KSPField(guiActiveEditor=true,guiName="Preview lights")] public bool previewLights;
 [KSPField(guiActive=true,guiActiveEditor=true,guiName="Vehicle lighting")] public string lightingStatus="Waiting for export data";
 sealed class Channel {public string Function;public float Range,Power,Angle;public Color Color;public bool Shadows;}
 sealed class Lamp {public Vector3 Position,Direction;public bool Point,Indicator;public Light Light;public readonly List<Channel> Channels=new List<Channel>();}
 sealed class Glow {public Renderer Renderer;public int Slot;public Color Color;public readonly List<KeyValuePair<string,float>> Signals=new List<KeyValuePair<string,float>>();public MaterialPropertyBlock Block,OverlayBlock;public bool Rear;public Renderer Overlay;public Material Material;}
 readonly List<Lamp> lamps=new List<Lamp>();readonly List<Glow> glows=new List<Glow>();
 readonly List<Material> ownedMaterials=new List<Material>();readonly List<GameObject> ownedObjects=new List<GameObject>();
 Texture2D lowCookie,highCookie;KDAEngine engine;bool built;float buildAfter;
 public bool Supported {get{return built&&(lamps.Count>0||glows.Count>0);}}
 public int LampCount {get{return lamps.Count;}}public int GlowCount {get{return glows.Count;}}
 public int ActiveLights {get;private set;}public float GlowLevel {get;private set;}
 public bool HeadlightsOn {get{return HighLogic.LoadedSceneIsEditor?previewLights:vessel!=null&&vessel.ActionGroups[KSPActionGroup.Light];}}
 [KSPEvent(guiActive=true,guiName="Toggle headlights")] public void ToggleHeadlights(){if(vessel!=null)vessel.ActionGroups.SetGroup(KSPActionGroup.Light,!HeadlightsOn);}
 [KSPAction("Headlights",KSPActionGroup.Light)] public void HeadlightsAction(KSPActionParam p){}
 [KSPAction("Toggle high beams")] public void HighAction(KSPActionParam p){highBeams=!highBeams;}
 [KSPEvent(guiActive=true,guiName="Left indicator")] public void Left(){indicators=indicators==1?0:1;}
 [KSPEvent(guiActive=true,guiName="Right indicator")] public void Right(){indicators=indicators==2?0:2;}
 [KSPEvent(guiActive=true,guiName="Hazard lights")] public void Hazard(){indicators=indicators==3?0:3;}
 [KSPAction("Left indicator")] public void LeftAction(KSPActionParam p){Left();}
 [KSPAction("Right indicator")] public void RightAction(KSPActionParam p){Right();}
 [KSPAction("Hazard lights")] public void HazardAction(KSPActionParam p){Hazard();}
 public override void OnStart(StartState state){base.OnStart(state);engine=part.FindModuleImplementing<KDAEngine>();buildAfter=Time.unscaledTime+.3f;}
 static float Number(ConfigNode n,string key,float fallback){float v;return float.TryParse(n.GetValue(key),NumberStyles.Float,CultureInfo.InvariantCulture,out v)&&!float.IsNaN(v)&&!float.IsInfinity(v)?v:fallback;}
 static Vector3 Vector(ConfigNode n,string key,Vector3 fallback){var text=n.GetValue(key);if(string.IsNullOrEmpty(text))return fallback;var parts=text.Split(',');float x,y,z;if(parts.Length!=3||!float.TryParse(parts[0],NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!float.TryParse(parts[1],NumberStyles.Float,CultureInfo.InvariantCulture,out y)||!float.TryParse(parts[2],NumberStyles.Float,CultureInfo.InvariantCulture,out z))return fallback;var v=new Vector3(x,y,z);return float.IsNaN(v.sqrMagnitude)||float.IsInfinity(v.sqrMagnitude)?fallback:v;}
 static Color ColorOf(ConfigNode n){var c=Vector(n,"color",Vector3.one);return new Color(c.x,c.y,c.z,1);}
 Texture2D Cookie(bool high){var t=new Texture2D(128,128,TextureFormat.RGBA32,false);t.wrapMode=TextureWrapMode.Clamp;var data=new Color[128*128];for(int y=0;y<128;y++)for(int x=0;x<128;x++){float u=(x-63.5f)/63.5f,v=(y-63.5f)/63.5f;float a=Mathf.Clamp01((1-Mathf.Sqrt(u*u+v*v))*5);if(!high)a*=Mathf.Clamp01((.10f-v)*14);data[y*128+x]=new Color(1,1,1,a);}t.SetPixels(data);t.Apply(false,true);return t;}
 bool Match(Material m,string name){return m!=null&&(m.name==name||m.name==name+" (Instance)");}
 void Build(){
  built=true;if(part.partInfo==null)return;ConfigNode config=null;foreach(var n in GameDatabase.Instance.GetConfigNodes("KDA_VEHICLE_LIGHTS"))if(n.GetValue("part")==part.partInfo.name){config=n;break;}
  if(config==null){lightingStatus="No exported lighting data";return;}
  lowCookie=Cookie(false);highCookie=Cookie(true);float rearPlane=Number(config,"rearLampPlane",float.NegativeInfinity);
  foreach(var n in config.GetNodes("LIGHT")){
   var pos=Vector(n,"position",Vector3.zero);var dir=Vector(n,"direction",Vector3.up).normalized;if(dir.sqrMagnitude<.5f||pos.sqrMagnitude>900)continue;
   bool point=n.GetValue("kind")=="POINTLIGHT";string function=n.GetValue("function")??"";bool indicator=function.StartsWith("signal_")||function=="hazard";Lamp lamp=null;
   // Current-car correction: some Automation rear fixtures are authored as
   // white headlamps. Their isolated surfaces provide brake glow, not beams.
   bool headlamp=function=="lowbeam"||function=="highbeam"||function=="lowhighbeam"||function=="running"||function=="runninglights";
   if(!float.IsNegativeInfinity(rearPlane)&&dir.y<-.4f&&(headlamp||pos.y<rearPlane&&(function=="brake"||function=="brakelights")))continue;
   foreach(var l in lamps)if(l.Point==point&&l.Indicator==indicator&&(l.Position-pos).sqrMagnitude<.0064f&&Vector3.Dot(l.Direction,dir)>.995f){lamp=l;break;}
   if(lamp==null){lamp=new Lamp{Position=pos,Direction=dir,Point=point,Indicator=indicator};lamps.Add(lamp);var go=new GameObject("KDA exported fixture light");ownedObjects.Add(go);go.transform.SetParent(part.transform,false);go.transform.localPosition=pos+dir*.035f;Vector3 up=Mathf.Abs(Vector3.Dot(dir,Vector3.back))>.95f?Vector3.up:Vector3.back;go.transform.localRotation=Quaternion.LookRotation(dir,up);lamp.Light=go.AddComponent<Light>();lamp.Light.type=point?LightType.Point:LightType.Spot;lamp.Light.enabled=false;lamp.Light.renderMode=LightRenderMode.Auto;lamp.Light.shadows=LightShadows.None;}
   lamp.Channels.Add(new Channel{Function=n.GetValue("function"),Range=Mathf.Clamp(Number(n,"range",10),.1f,150),Power=Mathf.Clamp(Number(n,"brightness",1),0,8),Angle=Mathf.Clamp(Number(n,"angle",45),5,160),Color=ColorOf(n),Shadows=n.GetValue("shadows")=="true"});
  }
  foreach(var n in config.GetNodes("SURFACE")){
   string name=n.GetValue("material"),url=n.GetValue("texture");if(string.IsNullOrEmpty(name)||string.IsNullOrEmpty(url)||!url.StartsWith("KDAVehicles/")||url.Contains(".."))continue;
   var texture=GameDatabase.Instance.GetTexture(url,false);if(texture==null){Debug.LogWarning("[KDA] Missing light emission texture "+url);continue;}
   foreach(var renderer in part.GetComponentsInChildren<MeshRenderer>(true)){
    if(renderer.GetComponentInParent<Part>()!=part)continue;var materials=renderer.sharedMaterials;
    for(int slot=0;slot<materials.Length;slot++){
     var original=materials[slot];if(!Match(original,name))continue;
     var glow=new Glow{Renderer=renderer,Slot=slot,Color=ColorOf(n)};
     bool rear=renderer.name.StartsWith("KDARearBrakeSkin_",StringComparison.Ordinal);
     if(rear){glow.Rear=true;glow.Color=new Color(1,.005f,.001f,1);glow.Signals.Add(new KeyValuePair<string,float>("brake",1));}
     else foreach(string line in n.GetValues("signal")){var items=line.Split(',');float weight;if(items.Length==2&&float.TryParse(items[1],NumberStyles.Float,CultureInfo.InvariantCulture,out weight)&&weight>0&&weight<=8)glow.Signals.Add(new KeyValuePair<string,float>(items[0],weight));}
     // Use a separate glow pass on rear lenses: transparent lamp shaders
     // can suppress material emission despite advertising those properties.
     if(!rear&&original.HasProperty("_Emissive")&&original.HasProperty("_EmissiveColor")){
      glow.Block=new MaterialPropertyBlock();renderer.GetPropertyBlock(glow.Block,slot);glow.Block.SetTexture("_Emissive",texture);glow.Block.SetColor("_EmissiveColor",Color.black);renderer.SetPropertyBlock(glow.Block,slot);
     }else{
      var filter=renderer.GetComponent<MeshFilter>();var shader=rear?Shader.Find("KSP/Emissive/Diffuse"):(Shader.Find("Particles/Additive")??Shader.Find("Legacy Shaders/Particles/Additive"));if(filter==null||filter.sharedMesh==null||shader==null)continue;
      // A separate glow pass leaves the exterior materials intact. Rear
      // groups contain only isolated lamp geometry; no collider is added.
      var go=new GameObject("KDA fixture glow");ownedObjects.Add(go);go.layer=renderer.gameObject.layer;go.transform.SetParent(renderer.transform,false);go.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
      var overlay=go.AddComponent<MeshRenderer>();overlay.shadowCastingMode=ShadowCastingMode.Off;overlay.receiveShadows=false;var slots=new Material[materials.Length];
      for(int i=0;i<slots.Length;i++){var mat=new Material(shader);ownedMaterials.Add(mat);mat.mainTexture=i==slot?texture:Texture2D.blackTexture;if(rear){mat.SetColor("_Color",Color.black);mat.SetTexture("_Emissive",i==slot?texture:Texture2D.blackTexture);mat.SetColor("_EmissiveColor",Color.black);}else mat.SetColor("_TintColor",Color.clear);mat.renderQueue=original.renderQueue+1;slots[i]=mat;if(i==slot)glow.Material=mat;}overlay.sharedMaterials=slots;overlay.enabled=false;glow.Overlay=overlay;if(rear)glow.OverlayBlock=new MaterialPropertyBlock();
     }
     glows.Add(glow);
    }
   }
  }
  lightingStatus=lamps.Count+" fixture lights / "+glows.Count+" glow surfaces";Debug.Log("[KDA] Lighting "+part.partInfo.name+": "+lightingStatus);
 }
 double Signal(string name,bool powered){return powered?LightingCore.Signal(name,HeadlightsOn,highBeams,vessel!=null&&vessel.ActionGroups[KSPActionGroup.Brakes],engine!=null&&engine.driveSelector<0,indicators,Time.time,engine!=null&&engine.engineRunning):0;}
 void Update(){
  if(!HighLogic.LoadedSceneIsFlight&&!HighLogic.LoadedSceneIsEditor)return;
  if(!built&&Time.unscaledTime>=buildAfter){try{Build();}catch(Exception e){built=true;lightingStatus="Lighting data unavailable";Debug.LogError("[KDA] Lighting: "+e);}}
  if(!built)return;bool powered=HighLogic.LoadedSceneIsEditor||vessel!=null&&!vessel.packed;var battery=part.Resources["ElectricCharge"];if(battery!=null&&battery.amount<=.001)powered=false;
  float distance=FlightCamera.fetch==null?0:Vector3.Distance(part.transform.position,FlightCamera.fetch.transform.position);if(distance>450)powered=false;
  ActiveLights=0;GlowLevel=0;int shadows=0,pixels=0;
  // Forward road beams get the limited pixel/shadow budget first. Every
  // emissive surface still responds, including lamps beyond the beam budget.
  for(int pass=0;pass<2;pass++)foreach(var lamp in lamps){
   bool front=Vector3.Dot(lamp.Direction,Vector3.up)>.4f;if((pass==0)!=front)continue;
   Channel chosen=null;float best=0;foreach(var c in lamp.Channels){float value=(float)Signal(c.Function,powered)*c.Power;if(value>best){best=value;chosen=c;}}
   bool on=chosen!=null&&ActiveLights<8;lamp.Light.enabled=on;if(!on)continue;ActiveLights++;
   lamp.Light.color=chosen.Color;lamp.Light.range=chosen.Range;lamp.Light.spotAngle=chosen.Angle;lamp.Light.intensity=Mathf.Clamp(best*3*brightness,0,8);
   lamp.Light.cookie=lamp.Point?null:(!lamp.Indicator&&front&&!highBeams?lowCookie:highCookie);
   bool road=front&&!lamp.Indicator;bool shadow=road&&chosen.Shadows&&shadows<2&&vessel==FlightGlobals.ActiveVessel;lamp.Light.shadows=shadow?LightShadows.Hard:LightShadows.None;if(shadow)shadows++;lamp.Light.renderMode=road&&pixels<2?LightRenderMode.ForcePixel:LightRenderMode.Auto;if(road)pixels++;
  }
  foreach(var glow in glows){
   float level=0;foreach(var s in glow.Signals)level+=(float)Signal(s.Key,powered)*s.Value;level=Mathf.Clamp01(level);GlowLevel=Mathf.Max(GlowLevel,level);Color color=glow.Color*(level*2*brightness);color.a=1;
   if(glow.Block!=null){glow.Block.SetColor("_EmissiveColor",color);glow.Renderer.SetPropertyBlock(glow.Block,glow.Slot);}
   else if(glow.Overlay!=null){glow.Overlay.enabled=level>.001f;if(glow.Rear){glow.OverlayBlock.SetColor("_EmissiveColor",color);glow.Overlay.SetPropertyBlock(glow.OverlayBlock,glow.Slot);}else glow.Material.SetColor("_TintColor",new Color(color.r*.5f,color.g*.5f,color.b*.5f,level));}
  }
 }
 void OnDestroy(){foreach(var go in ownedObjects)if(go!=null)Destroy(go);foreach(var mat in ownedMaterials)if(mat!=null)Destroy(mat);if(lowCookie!=null)Destroy(lowCookie);if(highCookie!=null)Destroy(highCookie);}
}
