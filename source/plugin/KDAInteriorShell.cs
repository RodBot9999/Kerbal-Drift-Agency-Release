using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A render-only, per-car IVA shell. Original exterior meshes/materials are
// never modified. All mesh/texture work happens once, when first entering IVA.
public sealed class KDAInteriorShell:MonoBehaviour {
 public Part Body;
 public bool Display=true; // Native comparison harness; not a tuning control.
 public int MeshCount {get;private set;}
 public int TriangleCount {get;private set;}
 public int GlassGroupsSkipped {get;private set;}
 public int WindowCount {get{return windows.Count;}}
 public int WindowTriangles {get;private set;}
 public int MaskCount {get{return masks.Count;}}
 public bool Visible {get{return root!=null&&root.activeSelf;}}
 public GameObject ShellRoot {get{return root;}}
 GameObject root;bool failed;
 readonly List<Mesh> meshes=new List<Mesh>();
 readonly List<Material> materials=new List<Material>();
 readonly Dictionary<Texture,Texture2D> masks=new Dictionary<Texture,Texture2D>();
 readonly Dictionary<Texture,Texture2D> windowMasks=new Dictionary<Texture,Texture2D>();
 sealed class Window {public MeshRenderer Renderer;public Vector3 Center,Normal;public MaterialPropertyBlock Properties=new MaterialPropertyBlock();}
 readonly List<Window> windows=new List<Window>();
 Bounds bounds;bool hasBounds;
 void LateUpdate(){
  if(Body==null||Body.vessel==null||CameraManager.Instance==null)return;
  var manager=CameraManager.Instance;var crew=manager.IVACameraActiveKerbal;
  bool active=manager.currentCameraMode==CameraManager.CameraMode.IVA&&crew!=null&&crew.InPart!=null&&crew.InPart.vessel==Body.vessel&&crew.InPart.FindModuleImplementing<KDARaceSeat>()!=null;
  if(active&&root==null&&!failed)Build(crew.InPart);
  if(root==null)return;root.SetActive(active&&Display);
  if(!active)return;
  root.transform.position=InternalSpace.WorldToInternal(Body.transform.position);
  root.transform.rotation=InternalSpace.WorldToInternal(Body.transform.rotation);
  root.transform.localScale=Body.transform.lossyScale;
  UpdateWindows();
 }
 static bool Glass(Material material){string shader=material.shader==null?"":material.shader.name.ToLowerInvariant();string name=material.name.ToLowerInvariant();return shader.Contains("translucent")||shader.Contains("transparent")||name.Contains("carwindows")||name.Contains("fixtureglass");}
 static bool CabinWindow(Material material){return material!=null&&material.name.IndexOf("window",StringComparison.OrdinalIgnoreCase)>=0&&material.name.IndexOf("fixtureglass",StringComparison.OrdinalIgnoreCase)<0;}
 Texture WindowMask(Texture source){
  if(source==null)return Texture2D.whiteTexture;Texture2D cached;if(windowMasks.TryGetValue(source,out cached))return cached;
  // Window masks only need a small copy: retain openings/UV coverage but
  // discard the nearly black exterior tint. Never read/modify source assets.
  float scale=Mathf.Min(1,512f/Mathf.Max(source.width,source.height));int width=Mathf.Max(1,Mathf.RoundToInt(source.width*scale)),height=Mathf.Max(1,Mathf.RoundToInt(source.height*scale));
  var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);Texture2D result=null;
  try {Graphics.Blit(source,target);RenderTexture.active=target;result=new Texture2D(width,height,TextureFormat.RGBA32,true);result.ReadPixels(new Rect(0,0,width,height),0,0);result.Apply();var pixels=result.GetPixels32();for(int i=0;i<pixels.Length;i++)pixels[i].r=pixels[i].g=pixels[i].b=255;result.SetPixels32(pixels);result.wrapMode=source.wrapMode;result.filterMode=FilterMode.Bilinear;result.name="KDA cabin window coverage";result.Apply(true,true);windowMasks[source]=result;return result;}
  catch {if(result!=null)Destroy(result);throw;}
  finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
 }
 Material WindowMaterial(Material original){
  var shader=Shader.Find("KSP/Alpha/Translucent Specular");if(shader==null)throw new InvalidOperationException("Stock translucent cabin glass shader unavailable");
  var material=new Material(shader);material.name="KDA cabin glass "+original.name;materials.Add(material);
  material.mainTexture=WindowMask(original.mainTexture);material.mainTextureScale=original.mainTextureScale;material.mainTextureOffset=original.mainTextureOffset;
  material.color=new Color(.55f,.64f,.68f,.075f);if(material.HasProperty("_SpecColor"))material.SetColor("_SpecColor",new Color(.22f,.24f,.25f,1));if(material.HasProperty("_Shininess"))material.SetFloat("_Shininess",.8f);
  material.renderQueue=3000;return material;
 }
 void AddWindows(MeshFilter filter,Material[] originals){
  var source=filter.sharedMesh;var input=source.vertices;var inputNormals=source.normals;var uv=source.uv;
  var frame=Body.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;var normalFrame=frame.inverse.transpose;
  var vertices=new Vector3[input.Length];var normals=new Vector3[input.Length];
  for(int i=0;i<input.Length;i++){vertices[i]=frame.MultiplyPoint3x4(input[i]);normals[i]=inputNormals.Length==input.Length?-normalFrame.MultiplyVector(inputNormals[i]).normalized:Vector3.zero;}
  for(int slot=0;slot<originals.Length&&slot<source.subMeshCount;slot++){
   if(!CabinWindow(originals[slot]))continue;var material=WindowMaterial(originals[slot]);var sourceTriangles=source.GetTriangles(slot);
   // Partition by surface direction, so side glass can catch a glancing
   // highlight without darkening the windshield. At most six batches/slot.
   var groups=new List<int>[6];var centers=new Vector3[6];var directions=new Vector3[6];var areas=new float[6];for(int i=0;i<6;i++)groups[i]=new List<int>();
   for(int t=0;t<sourceTriangles.Length;t+=3){int a=sourceTriangles[t],b=sourceTriangles[t+1],c=sourceTriangles[t+2];Vector3 cross=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);float area=cross.magnitude;if(area<1e-10f)continue;
    Vector3 direction=(normals[a]+normals[b]+normals[c]).normalized;if(direction.sqrMagnitude<.5f)direction=(frame.determinant<0?cross:-cross).normalized;
    int axis=Mathf.Abs(direction.x)>=Mathf.Abs(direction.y)&&Mathf.Abs(direction.x)>=Mathf.Abs(direction.z)?0:Mathf.Abs(direction.y)>=Mathf.Abs(direction.z)?1:2;int group=axis*2+(direction[axis]<0?1:0);
    groups[group].Add(a);groups[group].Add(frame.determinant<0?b:c);groups[group].Add(frame.determinant<0?c:b);centers[group]+=(vertices[a]+vertices[b]+vertices[c])*(area/3);directions[group]+=direction*area;areas[group]+=area;
   }
   for(int g=0;g<6;g++)if(groups[g].Count>0){var mesh=new Mesh();mesh.name="KDA inward cabin glass";if(input.Length>65535)mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=vertices;mesh.normals=normals;if(uv.Length==input.Length)mesh.uv=uv;mesh.triangles=groups[g].ToArray();if(inputNormals.Length!=input.Length)mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
    var go=new GameObject("KDA cabin window "+filter.name+" "+g);go.layer=16;go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    windows.Add(new Window{Renderer=renderer,Center=centers[g]/areas[g],Normal=directions[g].normalized});MeshCount++;TriangleCount+=groups[g].Count/3;WindowTriangles+=groups[g].Count/3;
   }
  }
 }
 void UpdateWindows(){
  if(InternalCamera.Instance==null)return;Vector3 eye=root.transform.InverseTransformPoint(InternalCamera.Instance.transform.position);
  foreach(var window in windows){float facing=Mathf.Abs(Vector3.Dot((eye-window.Center).normalized,window.Normal));float edge=1-Mathf.Clamp01(facing);float alpha=.075f+.105f*edge*edge;window.Properties.SetColor("_Color",new Color(.55f,.64f,.68f,alpha));window.Renderer.SetPropertyBlock(window.Properties);}
 }
 Texture2D Mask(Texture texture){
  if(texture==null)return null;Texture2D cached;if(masks.TryGetValue(texture,out cached))return cached;
  var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);
  Texture2D result=null;
  try {Graphics.Blit(texture,target);RenderTexture.active=target;result=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,true);result.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);result.Apply();var pixels=result.GetPixels32();for(int i=0;i<pixels.Length;i++){pixels[i].r=pixels[i].g=pixels[i].b=255;}result.SetPixels32(pixels);result.wrapMode=texture.wrapMode;result.filterMode=texture.filterMode;result.name="KDA interior original cutout mask";result.Apply(true,true);masks[texture]=result;return result;}
  catch {if(result!=null)Destroy(result);throw;}
  finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
 }
 Material Surface(Material original){
  string shaderName=original.shader==null?"":original.shader.name;bool cutout=shaderName.IndexOf("cut",StringComparison.OrdinalIgnoreCase)>=0||shaderName.IndexOf("alphatest",StringComparison.OrdinalIgnoreCase)>=0;
  var shader=Shader.Find(cutout?"KSP/Alpha/Cutoff Bumped":"KSP/Diffuse");if(shader==null)throw new InvalidOperationException("Stock KSP interior shader unavailable");
  var material=new Material(shader);material.name="KDA inner "+original.name;materials.Add(material);
  if(original.HasProperty("_MainTex")){material.mainTexture=original.mainTexture;material.mainTextureScale=original.mainTextureScale;material.mainTextureOffset=original.mainTextureOffset;}
  bool lining=original.name.IndexOf("carpaint",StringComparison.OrdinalIgnoreCase)>=0||original.name.IndexOf("chrome",StringComparison.OrdinalIgnoreCase)>=0;
  if(lining){material.mainTexture=cutout?Mask(original.mainTexture):Texture2D.whiteTexture;material.color=new Color(.035f,.035f,.035f,1);}
  else material.color=original.HasProperty("_Color")?original.color:Color.white;
  if(material.HasProperty("_Cutoff"))material.SetFloat("_Cutoff",original.HasProperty("_AlphaCutoff")?original.GetFloat("_AlphaCutoff"):original.HasProperty("_Cutoff")?original.GetFloat("_Cutoff"):.45f);
  // Retain UVs and alpha masks. A neutral normal texture leaves trim matte.
  return material;
 }
 Mesh Copy(Mesh source,Matrix4x4 frame){
  var input=source.vertices;var inputNormals=source.normals;var inputUv=source.uv;
  var vertices=new Vector3[input.Length*2];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];var normalFrame=frame.inverse.transpose;
  for(int i=0;i<input.Length;i++){vertices[i]=vertices[i+input.Length]=frame.MultiplyPoint3x4(input[i]);Vector3 normal=inputNormals.Length==input.Length?normalFrame.MultiplyVector(inputNormals[i]).normalized:Vector3.up;normals[i]=normal;normals[i+input.Length]=-normal;if(inputUv.Length==input.Length)uv[i]=uv[i+input.Length]=inputUv[i];if(!hasBounds){bounds=new Bounds(vertices[i],Vector3.zero);hasBounds=true;}else bounds.Encapsulate(vertices[i]);}
  var mesh=new Mesh();mesh.name="KDA inward body surfaces";if(vertices.Length>65535)mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.subMeshCount=source.subMeshCount;
  for(int s=0;s<source.subMeshCount;s++){var original=source.GetTriangles(s);var triangles=new int[original.Length*2];Array.Copy(original,triangles,original.Length);for(int i=0;i<original.Length;i+=3){triangles[original.Length+i]=original[i]+input.Length;triangles[original.Length+i+1]=original[i+2]+input.Length;triangles[original.Length+i+2]=original[i+1]+input.Length;}mesh.SetTriangles(triangles,s);TriangleCount+=triangles.Length/3;}
  mesh.RecalculateBounds();meshes.Add(mesh);return mesh;
 }
 void Add(string name,Mesh mesh,Material[] surfaces){var go=new GameObject(name);go.layer=16;go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=surfaces;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;MeshCount++;}
 void Panel(string name,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Material material){var mesh=new Mesh();mesh.name=name;mesh.vertices=new[]{a,b,c,d,a,b,c,d};var normal=Vector3.Cross(b-a,c-a).normalized;mesh.normals=new[]{normal,normal,normal,normal,-normal,-normal,-normal,-normal};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up,Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,1,2,0,2,3,4,6,5,4,7,6};mesh.RecalculateBounds();meshes.Add(mesh);TriangleCount+=4;Add(name,mesh,new[]{material});}
 void Floor(){
  if(!hasBounds)return;
  float rear=float.PositiveInfinity,front=float.NegativeInfinity,lower=float.NegativeInfinity;
  foreach(Part p in Body.vessel.parts)if(p.FindModuleImplementing<KDARaceSeat>()!=null){var position=Body.transform.InverseTransformPoint(p.transform.position);rear=Mathf.Min(rear,position.y-.65f);front=Mathf.Max(front,position.y+.85f);lower=Mathf.Max(lower,position.z+.035f);}
  if(float.IsInfinity(rear))return;rear=Mathf.Max(rear,bounds.min.y+.1f);front=Mathf.Min(front,bounds.max.y-.1f);float left=bounds.min.x*.86f,right=bounds.max.x*.86f;
  var material=new Material(Shader.Find("KSP/Diffuse"));material.name="KDA cabin floor";material.color=new Color(.025f,.025f,.025f);materials.Add(material);
  Panel("KDA cabin floor",new Vector3(left,rear,lower),new Vector3(right,rear,lower),new Vector3(right,front,lower),new Vector3(left,front,lower),material);
  Panel("KDA lower firewall",new Vector3(left,front,lower),new Vector3(right,front,lower),new Vector3(right,front,lower-.38f),new Vector3(left,front,lower-.38f),material);
 }
 void Build(Part seat){
  Clear();root=new GameObject("KDA interior body shell");root.layer=16;root.transform.SetParent(seat.internalModel.transform,false);
  try {
   foreach(var filter in Body.GetComponentsInChildren<MeshFilter>(true)){
    // Only the exported body groups: exclude fixtures, powertrain, moving
    // suspension, wheels and the render-only crew hierarchy.
    if(filter.GetComponentInParent<Part>()!=Body||filter.name.IndexOf("_body",StringComparison.OrdinalIgnoreCase)<0||filter.sharedMesh==null)continue;
    bool excluded=false;for(var t=filter.transform.parent;t!=null&&t!=Body.transform;t=t.parent)if(t.name=="Fixtures"||t.name=="RigidHardware"||t.name.StartsWith("KDASuspension_"))excluded=true;if(excluded)continue;
    var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null)continue;var original=renderer.sharedMaterials;bool glass=false;foreach(var material in original)if(material==null||Glass(material))glass=true;if(glass){AddWindows(filter,original);GlassGroupsSkipped++;continue;}
    var surfaces=new Material[original.Length];for(int i=0;i<surfaces.Length;i++)surfaces[i]=Surface(original[i]);
    Add(filter.name,Copy(filter.sharedMesh,Body.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix),surfaces);
   }
   if(MeshCount==0)throw new InvalidOperationException("No supported body surface groups");Floor();root.SetActive(false);
   Debug.Log("[KDAINTERIOR] Built "+Body.partInfo.name+" meshes="+MeshCount+" triangles="+TriangleCount+" windows="+WindowCount+" windowTriangles="+WindowTriangles+" skippedGlass="+GlassGroupsSkipped+" masks="+MaskCount+" colliders=0");
  }catch(Exception error){Debug.LogError("[KDAINTERIOR] Shell unavailable: "+error.Message);Clear();failed=true;}
 }
 void Clear(){if(root!=null)Destroy(root);root=null;foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);foreach(var material in materials)if(material!=null)Destroy(material);foreach(var mask in masks.Values)if(mask!=null)Destroy(mask);foreach(var mask in windowMasks.Values)if(mask!=null)Destroy(mask);meshes.Clear();materials.Clear();masks.Clear();windowMasks.Clear();windows.Clear();MeshCount=TriangleCount=GlassGroupsSkipped=WindowTriangles=0;hasBounds=false;}
 void OnDestroy(){Clear();}
}
