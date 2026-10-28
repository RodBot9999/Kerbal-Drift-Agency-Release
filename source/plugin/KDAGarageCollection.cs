using System;using System.IO;using System.Globalization;using UnityEngine;

public sealed partial class KDAGarage {
 double nextPreview;
 void ClearPreviews(){foreach(var car in cars)if(car.Preview!=null){Destroy(car.Preview);car.Preview=null;}}
 public int PreviewCount {get{int n=0;foreach(var c in cars)if(c.Preview!=null)n++;return n;}}
 void UpdateCollectionPreview(){
  if(view!=GarageView.Cars||Time.realtimeSinceStartup<nextPreview)return;nextPreview=Time.realtimeSinceStartup+.08;
  foreach(var car in cars){if(car.Preview!=null||car.PreviewFailed)continue;try{string name=ShipConstruction.GetPlayerCraftThumbnailName(Path.GetDirectoryName(car.Path),Path.GetFileNameWithoutExtension(car.Path));string path=Path.Combine(KSPUtil.ApplicationRootPath,"Thumbs",name+".png");if(File.Exists(path)){var texture=new Texture2D(2,2,TextureFormat.RGB24,false);if(texture.LoadImage(File.ReadAllBytes(path)))car.Preview=texture;else Destroy(texture);}if(car.Preview==null)car.Preview=RenderCarPreview(car.Path);car.PreviewFailed=car.Preview==null;}catch(Exception ex){car.PreviewFailed=true;Debug.LogWarning("[KDA Garage] Preview: "+ex.Message);}break;}
 }
 static Vector3 ParsePosition(string text){string[] f=(text??"0,0,0").Split(',');return f.Length<3?Vector3.zero:new Vector3(float.Parse(f[0],CultureInfo.InvariantCulture),float.Parse(f[1],CultureInfo.InvariantCulture),float.Parse(f[2],CultureInfo.InvariantCulture));}
 static Quaternion ParseRotation(string text){string[] f=(text??"0,0,0,1").Split(',');return f.Length<4?Quaternion.identity:new Quaternion(float.Parse(f[0],CultureInfo.InvariantCulture),float.Parse(f[1],CultureInfo.InvariantCulture),float.Parse(f[2],CultureInfo.InvariantCulture),float.Parse(f[3],CultureInfo.InvariantCulture));}
 Texture2D RenderCarPreview(string path){
  GameObject root=new GameObject("KDA Garage preview");Camera camera=null;RenderTexture rt=null;RenderTexture active=RenderTexture.active;Vector3 origin=new Vector3(10000,10000,10000);
  try{
   var config=ConfigNode.Load(path);if(config==null)return null;int count=0;
   foreach(var partNode in config.GetNodes("PART")){
    string id=partNode.GetValue("part")??"";int suffix=id.LastIndexOf('_');var info=PartLoader.getPartInfoByName(suffix>0?id.Substring(0,suffix):id);if(info==null||info.partPrefab==null)continue;
    Matrix4x4 pose=Matrix4x4.TRS(origin+ParsePosition(partNode.GetValue("pos")),ParseRotation(partNode.GetValue("rot")),Vector3.one);
    foreach(var renderer in info.partPrefab.GetComponentsInChildren<MeshRenderer>(true)){
     if(!renderer.enabled||renderer.gameObject.layer==16)continue;var mesh=renderer.GetComponent<MeshFilter>();if(mesh==null||mesh.sharedMesh==null)continue;
     bool visible=true;for(Transform t=renderer.transform;t!=null&&t!=info.partPrefab.transform;t=t.parent)if(!t.gameObject.activeSelf){visible=false;break;}if(!visible)continue;
     var matrix=pose*info.partPrefab.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
     var copy=new GameObject("mesh");copy.layer=31;copy.transform.SetParent(root.transform,false);copy.transform.position=matrix.MultiplyPoint3x4(Vector3.zero);copy.transform.rotation=matrix.rotation;copy.transform.localScale=matrix.lossyScale;
     copy.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;var target=copy.AddComponent<MeshRenderer>();target.sharedMaterials=renderer.sharedMaterials;target.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;target.receiveShadows=false;count++;
    }
   }
   if(count==0)return null;var renderers=root.GetComponentsInChildren<MeshRenderer>();Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
   var cameraObject=new GameObject("Preview camera");cameraObject.transform.SetParent(root.transform);camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.065f,.08f);camera.orthographic=true;camera.orthographicSize=Mathf.Max(.3f,bounds.extents.magnitude*.72f);camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.transform.position=bounds.center+new Vector3(1,.5f,-1).normalized*bounds.extents.magnitude*3;camera.transform.LookAt(bounds.center);
   for(int i=0;i<2;i++){var lightObject=new GameObject("Preview light");lightObject.transform.SetParent(root.transform);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.cullingMask=1<<31;light.intensity=i==0?1.15f:.65f;light.color=i==0?new Color(1,.96f,.9f):new Color(.74f,.86f,1);lightObject.transform.rotation=Quaternion.Euler(i==0?35:15,i==0?145:310,0);}
   rt=new RenderTexture(384,192,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(384,192,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,384,192),0,0);texture.Apply();return texture;
  }finally{RenderTexture.active=active;if(camera!=null)camera.targetTexture=null;if(rt!=null){rt.Release();Destroy(rt);}root.SetActive(false);Destroy(root);}
 }
 void DrawCollection(){
  Box(new Rect(0,610,1080,220),PanelColor);Box(new Rect(1080,100,360,730),PanelColor);
  GUI.Label(new Rect(22,614,180,27),"YOUR COLLECTION",sectionStyle);GUI.SetNextControlName("KDA.Text.CarSearch");search=GUI.TextField(new Rect(208,615,235,28),search,field);if(Button(new Rect(453,615,130,28),"REFRESH",false))RefreshCars();if(Button(new Rect(593,615,175,28),"PREBUILD CAR",false))OpenPrebuild();GUI.Label(new Rect(785,615,270,28),cars.Count+" saved / included cars",small);
  var visible=cars.FindAll(c=>c.Name.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0);carScroll=GUI.BeginScrollView(new Rect(12,652,1056,173),carScroll,new Rect(0,0,Mathf.Max(1040,visible.Count*198),148));
  float x=0;foreach(var car in visible){int index=cars.IndexOf(car);var tile=new Rect(x+4,0,186,124);if(Button(tile,"",index==selectedCar))RequestCar(index);if(car.Preview!=null)GUI.DrawTexture(new Rect(x+13,5,168,64),car.Preview,ScaleMode.ScaleToFit);else GUI.Label(new Rect(x+13,5,168,64),car.PreviewFailed?"Preview unavailable":"Loading preview…",small);GUI.Label(new Rect(x+12,71,170,37),car.Name,label);GUI.Label(new Rect(x+12,108,170,18),car.Source+" / "+car.Parts+" parts",small);int font=button.fontSize;button.fontSize=11;if(Button(new Rect(x+4,128,186,20),"DELETE CAR",false))RequestCarDelete(index);button.fontSize=font;x+=198;}GUI.EndScrollView();
  RefreshAnalysis();if(Button(new Rect(1100,118,151,32),"SPECIFICATIONS",!powerGraph))powerGraph=false;if(Button(new Rect(1263,118,151,32),"POWER GRAPH",powerGraph))powerGraph=true;
  if(body==null){GUI.Label(new Rect(1100,170,314,130),"Choose a car from your collection to view it and its specifications.",label);return;}
  GUI.Label(new Rect(1100,161,314,72),body.partInfo.title,heading);GUI.Label(new Rect(1100,243,314,62),engineDescription,label);
  if(powerGraph){DrawPowerGraph();return;}
  Stat(321,"Drive",drive);Stat(366,"Mass",carMass.ToString("0")+" kg");Stat(411,"Imported power",peakKw>0?peakKw.ToString("0")+" kW / "+(peakKw*1.341022).ToString("0")+" hp":"Not supplied");Stat(456,"Imported torque",peakTorque>0?peakTorque.ToString("0")+" Nm":"Not supplied");Stat(501,"Transmission",gearDescription);Stat(546,"Front weight",(frontWeight*100).ToString("0.0")+" %");Stat(591,"Wheelbase",wheelbase.ToString("0.00")+" m");Stat(636,"Track F / R",frontTrack.ToString("0.00")+" / "+rearTrack.ToString("0.00")+" m");
  GUI.Label(new Rect(1100,704,314,108),"Power and torque include the full exported boost. These are full-load crank ratings, not wheel power or a transient driving readout. Weight and balance describe the loaded editor craft.",small);
 }
 void Stat(float y,string title,string value){GUI.Label(new Rect(1100,y,140,35),title,small);GUI.Label(new Rect(1243,y,174,35),value,valueStyle);Box(new Rect(1100,y+38,314,1),new Color(.13f,.19f,.22f));}
}
