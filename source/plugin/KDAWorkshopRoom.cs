using System;
using System.Collections.Generic;
using UnityEngine;

// Original, deliberately simple rectangular workshop. Editor craft stays in the native SPH frame.
public sealed class KDAWorkshopRoom:IDisposable {
 sealed class State {public Renderer Renderer;public bool Enabled;}
 readonly List<State> scenery=new List<State>();readonly List<Material> materials=new List<Material>();readonly List<Texture2D> textures=new List<Texture2D>();
 readonly List<Mesh> meshes=new List<Mesh>();
 GameObject root;ShipConstruct bound;Part first;int count;float nextSceneryScan;Material concrete,wall,steel,green,black,lamp;
 readonly bool hideEditorScenery;
 public GameObject Root {get{return root;}}
 public Vector3 BayFocus {get{return root.transform.position+Vector3.up*1.2f;}}
 public KDAWorkshopRoom(bool hideScenery=true){
  hideEditorScenery=hideScenery;root=new GameObject("KDA Motorsport Workshop");root.layer=0;
  concrete=SurfaceMaterial(false);wall=SurfaceMaterial(true);steel=Material("Workshop steel",new Color(.29f,.32f,.34f));green=Material("KDA teal",new Color(.06f,.65f,.58f));black=Material("Workshop charcoal",new Color(.19f,.22f,.24f));lamp=Material("White LED diffusers",new Color(3.5f,3.7f,4),true);
  var red=Material("Toolbox red",new Color(.7f,.035f,.025f));var wood=Material("Workbench wood",new Color(.64f,.46f,.27f));var trim=Material("Brushed trim",new Color(.55f,.59f,.61f));
  Box("Smooth concrete floor",new Vector3(0,-.14f,0),new Vector3(20,.25f,22),concrete);
  // Single-sided interior panels let an orbiting camera see the car from beyond a wall.
  Panel("Back wall",new Vector3(0,3.75f,-11),new Vector2(20,7.5f),Quaternion.identity,wall);
  Panel("Front wall",new Vector3(0,3.75f,11),new Vector2(20,7.5f),Quaternion.Euler(0,180,0),wall);
  Panel("Left wall",new Vector3(-10,3.75f,0),new Vector2(22,7.5f),Quaternion.Euler(0,90,0),wall);
  Panel("Right wall",new Vector3(10,3.75f,0),new Vector2(22,7.5f),Quaternion.Euler(0,-90,0),wall);
  Panel("Ceiling",new Vector3(0,7.5f,0),new Vector2(20,22),Quaternion.Euler(90,0,0),steel);
  for(int side=-1;side<=1;side+=2){
   Panel("Lower wall panels",new Vector3(side*9.98f,1.3f,0),new Vector2(22,2.6f),Quaternion.Euler(0,-side*90,0),black);
   Box("Teal wall stripe",new Vector3(side*9.95f,2.65f,0),new Vector3(.025f,.14f,22),green);
   Box("Front teal stripe",new Vector3(0,2.65f,side*10.95f),new Vector3(20,.14f,.025f),green);
   Box("Parking bay edge",new Vector3(side*2.6f,.003f,0),new Vector3(.045f,.01f,7),green);
  }
  // Sectional door with simple seams and softly lit upper windows.
  Box("Sectional door",new Vector3(0,2.35f,-10.91f),new Vector3(8,4.7f,.1f),black);
  for(int i=1;i<7;i++)Box("Door seam",new Vector3(0,i*.65f,-10.84f),new Vector3(7.95f,.025f,.025f),steel);
  var window=Material("Door daylight",new Color(.65f,.82f,.9f),true);
  for(int i=0;i<4;i++){Box("Door window frame",new Vector3(-2.85f+i*1.9f,3.95f,-10.8f),new Vector3(1.65f,.65f,.06f),trim);Box("Door window",new Vector3(-2.85f+i*1.9f,3.95f,-10.75f),new Vector3(1.5f,.51f,.025f),window);}
  for(int side=-1;side<=1;side+=2)Box("Door rail",new Vector3(side*4.18f,2.4f,-10.72f),new Vector3(.1f,4.8f,.1f),trim);
  Box("Sign backing",new Vector3(0,5.65f,-10.85f),new Vector3(10,1.1f,.15f),black);
  Text("Kerbal Drift Agency",new Vector3(0,5.65f,-10.73f),.14f,new Color(.4f,1,.85f));
  Box("Neon sign underline",new Vector3(0,5.15f,-10.68f),new Vector3(7.5f,.035f,.035f),Material("Teal neon",new Color(.2f,3,2),true));
  // Cabinets stay against the walls, leaving the central orbit/build area clear.
  Box("Workbench",new Vector3(-7.2f,1.1f,-8.8f),new Vector3(4.6f,.12f,1.4f),wood);
  Box("Workbench base",new Vector3(-7.2f,.53f,-8.8f),new Vector3(4.4f,1,1.25f),black);
  Box("Pegboard",new Vector3(-7.2f,2.35f,-10.8f),new Vector3(4.8f,1.75f,.08f),steel);
  for(int i=0;i<10;i++){float x=-9.2f+i*.44f;Box("Tool shaft",new Vector3(x,2.4f,-10.69f),new Vector3(.055f,.45f+(i%3)*.08f,.05f),trim);Box("Tool head",new Vector3(x,2.68f,-10.69f),new Vector3(.15f,.09f,.06f),i%3==0?red:trim);}
  for(int i=0;i<4;i++){Box("Cabinet face",new Vector3(-8.9f+i*1.13f,.55f,-8.15f),new Vector3(1.02f,.9f,.04f),steel);Box("Cabinet handle",new Vector3(-8.9f+i*1.13f,.85f,-8.1f),new Vector3(.35f,.04f,.06f),trim);}
  Box("Cabinet counter",new Vector3(7.2f,1.15f,-8.8f),new Vector3(4.6f,.1f,1.4f),wood);
  Box("Lower cabinets",new Vector3(7.2f,.56f,-8.8f),new Vector3(4.4f,1.1f,1.25f),black);
  Box("Wall cabinets",new Vector3(7.2f,3.15f,-10.35f),new Vector3(4.6f,1.5f,1),black);
  for(int i=0;i<4;i++)Box("Wall cabinet handle",new Vector3(5.6f+i*1.05f,3.1f,-9.8f),new Vector3(.055f,.35f,.06f),trim);
  Box("Red rolling toolbox",new Vector3(6.5f,.8f,-5.7f),new Vector3(1.5f,1.3f,.85f),red);
  Box("Toolbox top",new Vector3(6.5f,1.48f,-5.7f),new Vector3(1.55f,.08f,.9f),black);
  for(int i=0;i<6;i++)Box("Toolbox drawer handle",new Vector3(6.5f,.35f+i*.18f,-5.24f),new Vector3(1.25f,.035f,.035f),trim);
  for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Cylinder("Toolbox caster",new Vector3(6.5f+x*.55f,.18f,-5.7f+z*.27f),new Vector3(.24f,.07f,.24f),Quaternion.Euler(0,0,90),black);
  for(int i=0;i<4;i++)Tire(new Vector3(8.8f,.56f+(i/2)*1.12f,-5.3f+(i%2)*1.15f));
  for(int z=-2;z<=2;z++)Box("Ceiling crossbeam",new Vector3(0,7.25f,z*4.2f),new Vector3(20,.22f,.18f),black);
  for(int x=-1;x<=1;x+=2)Box("Ceiling length beam",new Vector3(x*5,7.2f,0),new Vector3(.18f,.22f,22),black);
  for(int x=-1;x<=1;x++)Box("White LED strip",new Vector3(x*5.2f,7.02f,0),new Vector3(.12f,.05f,17),lamp);
  // A fixed small light budget; no realtime shadows or expensive reflection probes.
  for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)PointLight("White workshop light",new Vector3(x*4.2f,5.7f,z*4.5f),1.0f,17,new Color(.96f,.98f,1));
  var key=new GameObject("Soft workshop key");key.transform.SetParent(root.transform,false);key.transform.localRotation=Quaternion.Euler(58,205,0);var fill=key.AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.75f;fill.color=new Color(1,.98f,.94f);fill.shadows=LightShadows.None;
  PointLight("Neon spill",new Vector3(0,5.4f,-9.8f),.7f,6,new Color(.18f,1,.7f));
  if(hideEditorScenery)HideScenery();
 }
 Material Material(string name,Color color,bool unlit=false){var shader=Shader.Find(unlit?"Unlit/Color":"Standard")??Shader.Find("KSP/Diffuse");var m=new Material(shader);m.name=name;m.color=color;if(!unlit)Matte(m);materials.Add(m);return m;}
 static void Matte(Material material){
  // Explicit PBR values avoid a visual-mod shader conversion interpreting an
  // opaque texture's alpha as a polished surface. Keep both pipelines matte.
  if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0);
  if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.06f);
  if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.06f);
  if(material.HasProperty("_Shininess"))material.SetFloat("_Shininess",.01f);
  if(material.HasProperty("_SpecColor"))material.SetColor("_SpecColor",new Color(.015f,.015f,.015f,1));
  if(material.HasProperty("_SpecularHighlights"))material.SetFloat("_SpecularHighlights",0);
  if(material.HasProperty("_GlossyReflections"))material.SetFloat("_GlossyReflections",0);
  material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
 }
 static float Grain(int x,int y,int cells){unchecked{uint n=(uint)((x%cells+cells)%cells)*374761393u+(uint)((y%cells+cells)%cells)*668265263u;n=(n^(n>>13))*1274126177u;return ((n^(n>>16))&65535)/65535f;}}
 static float Noise(float u,float v,int cells){float x=u*cells,y=v*cells;int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);float a=x-ix,b=y-iy;a=a*a*(3-2*a);b=b*b*(3-2*b);return Mathf.Lerp(Mathf.Lerp(Grain(ix,iy,cells),Grain(ix+1,iy,cells),a),Mathf.Lerp(Grain(ix,iy+1,cells),Grain(ix+1,iy+1,cells),a),b);}
 Material SurfaceMaterial(bool masonry){
  const int size=512;var diffuse=new Texture2D(size,size,TextureFormat.RGB24,true);var normal=new Texture2D(size,size,TextureFormat.RGBA32,true,true);diffuse.name=masonry?"KDA matte painted wall":"KDA matte checkerboard tiles";normal.name=diffuse.name+" normal";
  var colors=new Color32[size*size];var bumps=new Color32[size*size];var heights=new float[size*size];
  for(int y=0;y<size;y++)for(int x=0;x<size;x++){
   float u=x/(float)size,v=y/(float)size;float broad=Noise(u,v,8),fine=Grain(x,y,size);float shade;
   if(masonry){shade=.86f+(broad-.5f)*.025f+(fine-.5f)*.009f;heights[y*size+x]=Noise(u,v,64)*.035f;}
   else{bool pale=((x/(size/2)+y/(size/2))&1)==0;int edge=Math.Min(Math.Min(x%(size/2),(size/2)-1-x%(size/2)),Math.Min(y%(size/2),(size/2)-1-y%(size/2)));bool grout=edge<1;shade=grout?.19f:(pale?.43f:.085f)+(broad-.5f)*.018f+(fine-.5f)*.006f;heights[y*size+x]=grout?-.08f:Noise(u,v,64)*.01f;}
   colors[y*size+x]=new Color(shade*1.015f,shade,shade*.985f,1);
  }
  for(int y=0;y<size;y++)for(int x=0;x<size;x++){float dx=heights[y*size+(x+1)%size]-heights[y*size+(x+size-1)%size],dy=heights[((y+1)%size)*size+x]-heights[((y+size-1)%size)*size+x];Vector3 n=new Vector3(-dx*2,-dy*2,1).normalized;bumps[y*size+x]=new Color(1,n.y*.5f+.5f,1,n.x*.5f+.5f);}
  diffuse.SetPixels32(colors);normal.SetPixels32(bumps);foreach(var texture in new[]{diffuse,normal}){texture.wrapMode=TextureWrapMode.Repeat;texture.filterMode=FilterMode.Trilinear;texture.anisoLevel=4;texture.Apply(true,true);textures.Add(texture);}
  var shader=Shader.Find("Standard")??Shader.Find("KSP/Bumped")??Shader.Find("KSP/Diffuse");var material=new Material(shader);material.name=masonry?"Workshop matte painted walls":"Workshop matte checkerboard floor";material.SetColor("_Color",masonry?new Color(.92f,.92f,.92f,1):Color.white);material.SetTexture("_MainTex",diffuse);if(material.HasProperty("_BumpMap")){material.SetTexture("_BumpMap",normal);material.EnableKeyword("_NORMALMAP");}
  Matte(material);
  if(!masonry){material.mainTextureScale=new Vector2(10,11);if(material.HasProperty("_BumpMap"))material.SetTextureScale("_BumpMap",new Vector2(10,11));}
  materials.Add(material);return material;
 }
 void Box(string name,Vector3 position,Vector3 size,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.Destroy(go.GetComponent<Collider>());}
 void Cylinder(string name,Vector3 position,Vector3 size,Quaternion rotation,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localRotation=rotation;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.Destroy(go.GetComponent<Collider>());}
 void PointLight(string name,Vector3 position,float intensity,float range,Color color){var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localPosition=position;var l=go.AddComponent<Light>();l.type=LightType.Point;l.range=range;l.intensity=intensity;l.color=color;l.shadows=LightShadows.None;}
 void Tire(Vector3 position){
  const int rings=24,segments=8;var vertices=new Vector3[rings*segments];var uv=new Vector2[vertices.Length];var triangles=new int[rings*segments*6];
  for(int i=0;i<rings;i++)for(int j=0;j<segments;j++){float a=i*2*Mathf.PI/rings,b=j*2*Mathf.PI/segments;vertices[i*segments+j]=new Vector3((.42f+.14f*Mathf.Cos(b))*Mathf.Cos(a),(.42f+.14f*Mathf.Cos(b))*Mathf.Sin(a),.19f*Mathf.Sin(b));uv[i*segments+j]=new Vector2(i/(float)rings,j/(float)segments);int p=(i*segments+j)*6,v=i*segments+j,n=((i+1)%rings)*segments+j,k=i*segments+(j+1)%segments,m=((i+1)%rings)*segments+(j+1)%segments;triangles[p]=v;triangles[p+1]=n;triangles[p+2]=k;triangles[p+3]=k;triangles[p+4]=n;triangles[p+5]=m;}
  var mesh=new Mesh();mesh.name="KDA workshop tire";mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
  var go=new GameObject("Spare tire");go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,90,0);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=black;
 }
 void Panel(string name,Vector3 position,Vector2 size,Quaternion rotation,Material material){
  var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
  var mesh=new Mesh();mesh.name="KDA workshop panel";mesh.vertices=new[]{new Vector3(-size.x/2,-size.y/2,0),new Vector3(size.x/2,-size.y/2,0),new Vector3(size.x/2,size.y/2,0),new Vector3(-size.x/2,size.y/2,0)};mesh.normals=new[]{Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward};mesh.uv=new[]{Vector2.zero,new Vector2(size.x/2,0),size/2,new Vector2(0,size.y/2)};mesh.tangents=new[]{new Vector4(1,0,0,1),new Vector4(1,0,0,1),new Vector4(1,0,0,1),new Vector4(1,0,0,1)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();meshes.Add(mesh);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
 }
 void Text(string text,Vector3 position,float size,Color color){var go=new GameObject("Workshop neon sign");go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,180,0);var t=go.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("Arial.ttf");t.fontSize=96;t.fontStyle=FontStyle.Bold;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;t.text=text;t.font.RequestCharactersInTexture(text,96,FontStyle.Bold);var neon=new Material(t.font.material);neon.name="Kerbal Drift Agency neon lettering";if(neon.HasProperty("_Color"))neon.SetColor("_Color",new Color(.35f,3,2));materials.Add(neon);var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=neon;float width=renderer.bounds.size.x;if(width>.01f)go.transform.localScale=Vector3.one*(8.8f/width);}
 void HideScenery(){
  foreach(var state in scenery)if(state.Renderer!=null)state.Renderer.enabled=false;
  if(Time.realtimeSinceStartup<nextSceneryScan)return;nextSceneryScan=Time.realtimeSinceStartup+.5f;
  // Stock workers and exterior scenery can live outside editorSceneryRoot.
  var field=typeof(EditorDriver).GetField("editorSceneryRoot",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);var scene=field==null?null:field.GetValue(EditorDriver.fetch) as Transform;
  foreach(var r in UnityEngine.Object.FindObjectsOfType<Renderer>()){
   if(r.gameObject.layer==5||r is SpriteRenderer||r.GetComponent<TextMesh>()!=null||r.transform.IsChildOf(root.transform)||r.GetComponentInParent<Part>()!=null||r.GetComponentInParent<Canvas>()!=null)continue;
   bool worker=r is SkinnedMeshRenderer;for(var t=r.transform;t!=null&&!worker;t=t.parent)worker=t.name.IndexOf("kerbal",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("groundcrew",StringComparison.OrdinalIgnoreCase)>=0;
   // Leave editor gizmos, preview renderers and other interactive tools alone.
   if(!worker&&(scene==null||!r.transform.IsChildOf(scene)))continue;
   bool known=false;foreach(var s in scenery)if(s.Renderer==r){known=true;break;}if(!known)scenery.Add(new State{Renderer=r,Enabled=r.enabled});r.enabled=false;
  }
 }
 public void Update(ShipConstruct ship){
  if(hideEditorScenery)HideScenery();Part current=ship!=null&&ship.parts.Count>0?ship.parts[0]:null;int n=ship==null?0:ship.parts.Count;
  if(ship==bound&&current==first&&n==count)return;bound=ship;first=current;count=n;
  Bounds bounds=new Bounds(new Vector3(0,5,0),Vector3.zero);bool found=false;
  if(ship!=null)foreach(var p in ship.parts)if(p!=null)foreach(var r in p.GetComponentsInChildren<Renderer>(false)){if(!r.enabled||(!(r is MeshRenderer)&&!(r is SkinnedMeshRenderer)))continue;if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);}
  root.transform.position=found?new Vector3(bounds.center.x,bounds.min.y-.02f,bounds.center.z):Vector3.zero;
 }
 public void Dispose(){foreach(var state in scenery)if(state.Renderer!=null)state.Renderer.enabled=state.Enabled;scenery.Clear();if(root!=null){root.SetActive(false);UnityEngine.Object.Destroy(root);}foreach(var mesh in meshes)UnityEngine.Object.Destroy(mesh);foreach(var m in materials)UnityEngine.Object.Destroy(m);foreach(var t in textures)UnityEngine.Object.Destroy(t);root=null;meshes.Clear();materials.Clear();textures.Clear();}
}
