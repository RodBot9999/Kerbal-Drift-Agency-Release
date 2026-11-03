using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Resurfaced 1.2's cutout surface shader has no ShadowCaster pass. Supply
// masked depth/shadows without changing its visible PBR color or filling holes.
public sealed class KDAExteriorDepth:MonoBehaviour {
 sealed class Companion {public MeshRenderer Source,Depth;}
 readonly List<Companion> companions=new List<Companion>();
 readonly List<Material> owned=new List<Material>();
 readonly List<GameObject> objects=new List<GameObject>();
 Material invisible;bool built;
 public int MaterialCount {get;private set;}
 public int QueueCorrections {get;private set;}
 public static bool NeedsDepth(string shader,int shadowPass){return shader=="Resurfaced/StandardAlphaTest"&&shadowPass<0;}
 public static int ColorQueue(int original,bool deferred){return deferred&&original>2500&&original<3000?2450:original;}
 public static int Ensure(Part body){
  if(body==null)return 0;
  var helper=body.GetComponent<KDAExteriorDepth>();if(helper==null)helper=body.gameObject.AddComponent<KDAExteriorDepth>();
  if(!helper.built)helper.Build(body);return helper.QueueCorrections;
 }
 static bool UsesDeferred(){foreach(var c in Camera.allCameras)if(c!=null&&c.enabled&&c.gameObject.activeInHierarchy&&c.cameraType!=CameraType.Reflection&&(c.actualRenderingPath==RenderingPath.DeferredShading||c.actualRenderingPath==RenderingPath.DeferredLighting))return true;return false;}
 void Build(Part body){
  built=true;bool deferred=UsesDeferred();Shader depthShader=Shader.Find("Standard");
  // Do not replace working PBR materials if a compatible depth shader is absent.
  if(depthShader==null)return;
  var template=new Material(depthShader);if(template.FindPass("ShadowCaster")<0){Destroy(template);return;}
  Destroy(template);
  var seen=new HashSet<MeshRenderer>();var changed=new HashSet<Material>();
  var parts=body.vessel==null?new List<Part>{body}:body.vessel.parts;
  foreach(var part in parts)foreach(var filter in part.GetComponentsInChildren<MeshFilter>(true)){
   var renderer=filter.GetComponent<MeshRenderer>();
   if(renderer==null||filter.sharedMesh==null||renderer.gameObject.layer==16||!seen.Add(renderer))continue;
   var original=renderer.sharedMaterials;bool needed=false;
   foreach(var material in original){
    if(material==null||material.shader==null)continue;
    if(NeedsDepth(material.shader.name,material.FindPass("ShadowCaster")))needed=true;
    if(material.shader.name=="Resurfaced/StandardAlphaTest"&&changed.Add(material)){
     int queue=ColorQueue(material.renderQueue,deferred);if(queue!=material.renderQueue){material.renderQueue=queue;QueueCorrections++;}
    }
   }
   if(!needed||renderer.shadowCastingMode==ShadowCastingMode.Off)continue;
   var surfaces=new Material[original.Length];
   for(int i=0;i<surfaces.Length;i++){
    var source=original[i];
    if(source==null||source.shader==null||!NeedsDepth(source.shader.name,source.FindPass("ShadowCaster"))){
     if(invisible==null){invisible=Masked(depthShader);invisible.name="KDA masked-out depth slot";invisible.mainTexture=Texture2D.blackTexture;invisible.color=Color.clear;invisible.SetFloat("_Cutoff",.5f);owned.Add(invisible);}
     surfaces[i]=invisible;continue;
    }
    var depth=Masked(depthShader);depth.name="KDA masked depth "+source.name;
    depth.mainTexture=source.mainTexture;depth.mainTextureScale=source.mainTextureScale;depth.mainTextureOffset=source.mainTextureOffset;
    depth.color=source.HasProperty("_Color")?source.color:Color.white;
    depth.SetFloat("_Cutoff",source.HasProperty("_AlphaCutoff")?source.GetFloat("_AlphaCutoff"):.5f);depth.renderQueue=2450;
    owned.Add(depth);surfaces[i]=depth;MaterialCount++;
   }
   var go=new GameObject("KDA masked exterior depth");go.layer=renderer.gameObject.layer;go.transform.SetParent(filter.transform,false);
   // ShadowsOnly is excluded from Unity's camera-depth collection. Keep this
   // renderer enabled, but disable every visible pass on its materials.
   go.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;var caster=go.AddComponent<MeshRenderer>();caster.sharedMaterials=surfaces;caster.shadowCastingMode=ShadowCastingMode.On;caster.receiveShadows=false;caster.enabled=renderer.enabled;caster.forceRenderingOff=renderer.forceRenderingOff;
   objects.Add(go);companions.Add(new Companion{Source=renderer,Depth=caster});
  }
  if(MaterialCount>0||QueueCorrections>0)Debug.Log("[KDARENDER] Masked depth/shadows="+MaterialCount+", deferred queue fixes="+QueueCorrections+", deferred="+deferred+" for "+(body.partInfo==null?body.name:body.partInfo.name));
 }
 static Material Masked(Shader shader){var m=new Material(shader);m.SetFloat("_Mode",1);m.SetOverrideTag("RenderType","TransparentCutout");m.SetInt("_SrcBlend",1);m.SetInt("_DstBlend",0);m.SetInt("_ZWrite",1);m.DisableKeyword("_ALPHABLEND_ON");m.DisableKeyword("_ALPHAPREMULTIPLY_ON");m.EnableKeyword("_ALPHATEST_ON");foreach(string pass in new[]{"FORWARD","FORWARD_DELTA","DEFERRED","META","ForwardBase","ForwardAdd","Deferred","Meta"})m.SetShaderPassEnabled(pass,false);m.renderQueue=2450;return m;}
 void LateUpdate(){foreach(var p in companions)if(p.Source!=null&&p.Depth!=null){p.Depth.enabled=p.Source.enabled;p.Depth.forceRenderingOff=p.Source.forceRenderingOff;}}
 void OnDestroy(){foreach(var go in objects)if(go!=null)Destroy(go);foreach(var m in owned)if(m!=null)Destroy(m);}
}
