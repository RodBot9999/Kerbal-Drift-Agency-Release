using System;
using System.Collections.Generic;
using UnityEngine;

// Editor-only linking; inversion stays side-specific. Flight overrides never propagate.
public class KDAAxle : PartModule {
 [KSPField(guiActiveEditor=true,guiName="Linked tire group")] public string axle="Attach to vehicle";
 readonly Dictionary<string,object> cachedValues=new Dictionary<string,object>();
 Part Body {get{return part.parent!=null&&part.parent.partInfo!=null&&part.parent.FindModuleImplementing<KDAEngine>()!=null?part.parent:null;}}
 bool Front(Part p){return Body.transform.InverseTransformPoint(p.transform.position).y>0;}
 bool Eligible(BaseField f){return f.guiActiveEditor&&f.name.IndexOf("invert",StringComparison.OrdinalIgnoreCase)<0&&f.name.IndexOf("direction",StringComparison.OrdinalIgnoreCase)<0;}
 public void Capture(){cachedValues.Clear();foreach(PartModule m in part.Modules)foreach(BaseField f in m.Fields)if(Eligible(f)){object value=f.GetValue(m);if(value is float||value is bool||value is int)cachedValues[m.moduleName+"/"+f.name]=value;}}
 void Update(){
  if(!HighLogic.LoadedSceneIsEditor||Body==null)return;
  axle=Front(part)?"Front axle":"Rear axle";
  if(cachedValues.Count==0){Capture();return;}
  foreach(PartModule m in part.Modules)foreach(BaseField f in m.Fields){
   if(!Eligible(f))continue;object value=f.GetValue(m),before;string key=m.moduleName+"/"+f.name;
   if(!cachedValues.TryGetValue(key,out before)||Equals(before,value))continue;
   foreach(Part p in Body.children){if(p==part||Front(p)!=Front(part))continue;var link=p.FindModuleImplementing<KDAAxle>();if(link==null)continue;
    foreach(PartModule other in p.Modules)if(other.moduleName==m.moduleName){BaseField target=other.Fields[f.name];if(target!=null)target.SetValue(value,other);}
    link.Capture();
   }
  }
  Capture();
 }
}

