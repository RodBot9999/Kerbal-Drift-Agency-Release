using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Weight assignment is done once. Each frame changes only a travel transform.
public partial class KDASuspensionVisual : PartModule {
 [KSPField] public string visualRoot="",hubReference="0,0,0",fixedPoints="",movingPoints="";
 Transform frame,movingBone,wheelPivot;Vector3 hub;int waitFrames;bool initialized;
 readonly List<Mesh> ownedMeshes=new List<Mesh>();
 static Vector3 Parse(string text){string[] s=text.Split(',');return new Vector3(float.Parse(s[0],CultureInfo.InvariantCulture),float.Parse(s[1],CultureInfo.InvariantCulture),float.Parse(s[2],CultureInfo.InvariantCulture));}
 static Vector3[] Points(string text){string[] s=text.Split(';');Vector3[] p=new Vector3[s.Length];for(int i=0;i<s.Length;i++)p[i]=Parse(s[i]);return p;}
 static float Distance(Vector3 p,Vector3[] points){float d=float.MaxValue;for(int i=0;i<points.Length;i++)d=Mathf.Min(d,(p-points[i]).sqrMagnitude);return Mathf.Sqrt(d);}
 void Initialize(){
  Transform root=part.FindModelTransform(visualRoot);if(root==null)return;frame=root.parent;hub=Parse(hubReference);
  Vector3[] fixedNodes=Points(fixedPoints),hubNodes=Points(movingPoints);
  movingBone=new GameObject(visualRoot+"_travel").transform;movingBone.SetParent(frame,false);
  foreach(MeshFilter filter in root.GetComponentsInChildren<MeshFilter>()){
   var original=filter.GetComponent<MeshRenderer>();if(original==null||filter.sharedMesh==null)continue;
   Mesh mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);Vector3[] v=mesh.vertices;BoneWeight[] weights=new BoneWeight[v.Length];
   Matrix4x4 toFrame=frame.worldToLocalMatrix*filter.transform.localToWorldMatrix;
   for(int i=0;i<v.Length;i++){
    Vector3 p=toFrame.MultiplyPoint3x4(v[i]);float a=Distance(p,fixedNodes),b=Distance(p,hubNodes);
    float t=Mathf.SmoothStep(0,1,a/Mathf.Max(.00001f,a+b));
    weights[i]=new BoneWeight{boneIndex0=0,boneIndex1=1,weight0=1-t,weight1=t};
   }
   mesh.boneWeights=weights;mesh.bindposes=new[]{frame.worldToLocalMatrix*filter.transform.localToWorldMatrix,movingBone.worldToLocalMatrix*filter.transform.localToWorldMatrix};
   var skin=filter.gameObject.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterials=original.sharedMaterials;skin.bones=new[]{frame,movingBone};skin.rootBone=frame;skin.quality=SkinQuality.Bone2;
   Bounds bounds=mesh.bounds;bounds.Expand(.6f);skin.localBounds=bounds;skin.updateWhenOffscreen=false;
   skin.shadowCastingMode=original.shadowCastingMode;skin.receiveShadows=original.receiveShadows;original.enabled=false;ownedMeshes.Add(mesh);
  }
  Debug.Log("[KDAVisualSuspension] "+visualRoot+" skins="+ownedMeshes.Count+" fixed="+fixedNodes.Length+" moving="+hubNodes.Length);
 }
 void LateUpdate(){
  if(!HighLogic.LoadedSceneIsFlight||part==null||part.packed)return;
  if(!initialized){if(++waitFrames<8)return;initialized=true;try{Initialize();}catch(Exception e){Debug.LogError("[KDAVisualSuspension] "+e.Message);} }
  if(movingBone==null||frame==null)return;
  if(wheelPivot==null){
   float best=.45f*.45f;
   foreach(Part p in part.children){
    if(p.FindModuleImplementing<KDASuspension>()==null)continue;
    float d=(frame.InverseTransformPoint(p.transform.position)-hub).sqrMagnitude;
    if(d>=best)continue;var pivot=p.FindModelTransform("suspensionPivot");if(pivot!=null){wheelPivot=pivot;best=d;}
   }
  }
  if(wheelPivot==null)return;
  var wheel=wheelPivot.GetComponentInParent<Part>();var su=wheel==null?null:wheel.FindModuleImplementing<KDASuspension>();
  if(su!=null&&su.LongTravel){EnsureKitRig(part.FindModelTransform(visualRoot),frame,hub,su.Travel,su.suspensionKit);if(kitRig!=null){kitRig.SetTrackExtension(su.trackExtensionMm);kitRig.Draw(frame.InverseTransformPoint(wheelPivot.position));}return;}StopGarageRig();
  // Body Z is the suspension axis. Ignore toe, spin and lateral part flex.
  float travel=Mathf.Clamp(frame.InverseTransformPoint(wheelPivot.position).z-hub.z,-.25f,.25f);
  movingBone.localPosition=new Vector3(0,0,travel);
 }
 void OnDestroy(){StopGarageRig();foreach(Mesh mesh in ownedMeshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(movingBone!=null)UnityEngine.Object.Destroy(movingBone.gameObject);}
}
