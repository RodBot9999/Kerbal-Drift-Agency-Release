using System;using System.Collections.Generic;using UnityEngine;
public partial class KDASuspensionVisual {
 KDALongTravelRig kitRig;float rigTravel;int rigKit;Part garageWheel;
 void EnsureKitRig(Transform root,Transform reference,Vector3 origin,float travel,int kit){if(root==null)return;if(kitRig!=null&&(Mathf.Abs(rigTravel-travel)>.001f||rigKit!=kit))StopGarageRig();if(kitRig==null){rigTravel=travel;rigKit=kit;kitRig=new KDALongTravelRig(root,reference,origin,travel,kit);}}
 public void UpdateGarageRig(Part wheel,Transform pivot,bool show,float travel,int kit,float trackMm){
  Transform root=part.FindModelTransform(visualRoot);if(root==null)return;var reference=root.parent;Vector3 origin=Parse(hubReference);
  if(garageWheel==null){float best=float.MaxValue;foreach(var candidate in part.children){if(candidate.FindModuleImplementing<KDASuspension>()==null)continue;float d=(reference.InverseTransformPoint(candidate.transform.position)-origin).sqrMagnitude;if(d<best){best=d;garageWheel=candidate;}}}
  if(garageWheel!=wheel)return;if(!show){StopGarageRig();return;}EnsureKitRig(root,reference,origin,travel,kit);kitRig.SetTrackExtension(trackMm);kitRig.Draw(reference.InverseTransformPoint(pivot.position));
 }
 public void StopGarageRig(){if(kitRig!=null){kitRig.Dispose();kitRig=null;}}
 public bool HasArticulatedKit {get{return kitRig!=null;}}
 public float VisualArmLengthError {get{return kitRig==null?0:kitRig.ArmLengthError;}}
}

// Optional replacement hardware: parallel articulated wishbones and a
// telescoping damper/coil. Imported meshes are restored when the kit is removed.
// These are visual links, not an additional physics or joint solver.
public sealed class KDALongTravelRig:IDisposable {
 readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();readonly GameObject root;readonly Transform[] arms=new Transform[4];readonly Transform shockBody,shaft,coil,upperJoint,lowerJoint;
 readonly Material steel,spring;readonly Mesh coilMesh;readonly Vector3 hub;readonly float length,branch=.13f,side;readonly Transform reference;
 public float ArmLengthError {get;private set;}
 public KDALongTravelRig(Transform original,Transform frame,Vector3 origin,float travel,int kit){
  reference=frame;hub=origin;length=(float)LongTravelCore.ArmLength(travel);side=Mathf.Sign(origin.x);if(side==0)side=1;
  foreach(var r in original.GetComponentsInChildren<Renderer>(true)){hidden[r]=r.enabled;r.enabled=false;}
  root=new GameObject("KDA "+(kit==2?"Desert":"Rally")+" articulated suspension");root.transform.SetParent(frame,false);
  var shader=Shader.Find("KSP/Specular")??Shader.Find("Standard")??Shader.Find("KSP/Diffuse");steel=new Material(shader);steel.color=new Color(.18f,.20f,.22f);spring=new Material(shader);spring.color=kit==2?new Color(.95f,.55f,.08f):new Color(.85f,.12f,.07f);
  for(int i=0;i<4;i++)arms[i]=Primitive("Rigid wishbone link",PrimitiveType.Cylinder,steel);
  shockBody=Primitive("Damper body",PrimitiveType.Cylinder,spring);shaft=Primitive("Telescoping shaft",PrimitiveType.Cylinder,steel);upperJoint=Primitive("Upper pivot",PrimitiveType.Sphere,steel);lowerJoint=Primitive("Lower pivot",PrimitiveType.Sphere,steel);
  coilMesh=Coil();var c=new GameObject("Coil spring");c.transform.SetParent(root.transform,false);c.AddComponent<MeshFilter>().sharedMesh=coilMesh;c.AddComponent<MeshRenderer>().sharedMaterial=spring;coil=c.transform;
 }
 Transform Primitive(string name,PrimitiveType type,Material material){var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(root.transform,false);var c=o.GetComponent<Collider>();if(c!=null){c.enabled=false;UnityEngine.Object.Destroy(c);}o.GetComponent<Renderer>().sharedMaterial=material;return o.transform;}
 static void Rod(Transform t,Vector3 a,Vector3 b,float radius){Vector3 d=b-a;t.localPosition=(a+b)*.5f;t.localRotation=Quaternion.FromToRotation(Vector3.up,d.normalized);t.localScale=new Vector3(radius*2,d.magnitude*.5f,radius*2);}
 public void Draw(Vector3 movingHub){
  float error=0;for(int axle=0;axle<2;axle++){float z=axle==0?-.10f:.10f;Vector3 outer=movingHub+new Vector3(0,0,z),inner=hub+new Vector3(-side*length,0,z);
   for(int branchIndex=0;branchIndex<2;branchIndex++){Vector3 at=inner+new Vector3(0,branchIndex==0?-branch:branch,0);Rod(arms[axle*2+branchIndex],at,outer,.014f);}
   // Both branches preserve their length as the parallel arms rotate. Track
   // extension changes the installed arm length, rather than stretching it.
   float vertical=movingHub.z-hub.z,horizontal=movingHub.x-(hub.x-side*length);float actual=Mathf.Sqrt(vertical*vertical+horizontal*horizontal);
   float expected=length+trackExtension;
   error=Mathf.Max(error,Mathf.Abs(actual-expected));
  }
  ArmLengthError=error;
  Vector3 top=hub+new Vector3(-side*length*.65f,0,-.34f),bottom=movingHub+new Vector3(-side*.06f,0,.07f),axis=bottom-top;float total=axis.magnitude;Vector3 dir=axis.normalized;float barrel=Mathf.Min(.25f,total*.7f);
  Rod(shockBody,top,top+dir*barrel,.034f);Rod(shaft,top+dir*barrel,bottom,.012f);
  upperJoint.localPosition=top;lowerJoint.localPosition=bottom;upperJoint.localScale=lowerJoint.localScale=Vector3.one*.055f;
  coil.localPosition=top;coil.localRotation=Quaternion.FromToRotation(Vector3.up,dir);coil.localScale=new Vector3(1,Mathf.Max(.04f,total*.85f),1);
 }
 float trackExtension;
 public void SetTrackExtension(float mm){trackExtension=Mathf.Clamp(mm,0,350)*.001f;}
 static Mesh Coil(){const int steps=96,sides=6;var verts=new Vector3[(steps+1)*sides];var triangles=new int[steps*sides*6];var normals=new Vector3[verts.Length];
  for(int i=0;i<=steps;i++){float t=i/(float)steps,angle=t*Mathf.PI*2*7;Vector3 center=new Vector3(Mathf.Cos(angle)*.052f,t,Mathf.Sin(angle)*.052f);for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides;Vector3 normal=new Vector3(Mathf.Cos(angle)*Mathf.Cos(a),Mathf.Sin(a),Mathf.Sin(angle)*Mathf.Cos(a));int n=i*sides+j;verts[n]=center+normal*.006f;normals[n]=normal;if(i==steps)continue;int next=i*sides+(j+1)%sides,k=(i*sides+j)*6;triangles[k]=n;triangles[k+1]=n+sides;triangles[k+2]=next;triangles[k+3]=next;triangles[k+4]=n+sides;triangles[k+5]=next+sides;}}
  var m=new Mesh();m.name="KDA coil spring";m.vertices=verts;m.normals=normals;m.triangles=triangles;m.RecalculateBounds();return m;
 }
 public void Dispose(){foreach(var p in hidden)if(p.Key!=null)p.Key.enabled=p.Value;if(root!=null){root.SetActive(false);UnityEngine.Object.Destroy(root);}if(coilMesh!=null)UnityEngine.Object.Destroy(coilMesh);if(steel!=null)UnityEngine.Object.Destroy(steel);if(spring!=null)UnityEngine.Object.Destroy(spring);}
}
