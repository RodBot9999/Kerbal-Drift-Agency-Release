using System;using UnityEngine;
public sealed partial class KDAGarage {
 void BindExtraTuning(){if(body==null)return;var engine=body.FindModuleImplementing<KDAEngine>();if(engine==null)return;
  AddExtra(engine,"brakePowerPercent","Service braking power","%",0,200,5,"100% is the original service brake torque.");
  AddExtra(engine,"afterfireTune","Afterfire tune","",0,2,1,"");
  AddExtra(engine,"afterfireAmount","Pops / bangs frequency","x",0,3,.1f,"0 disables afterfire. Higher settings add more events.");
  AddExtra(engine,"afterfireVolume","Pops / bangs sound","",0,1,.05f,"Separate sound level; also respects ship/engine volume.");
  AddExtra(engine,"flameSize","Flame size","x",.25f,3,.05f,"Scales particle width and flame reach.");
  AddExtra(engine,"flameDuration","Flame duration","x",.5f,3,.1f,"Scales each burst's lifetime; 1x is the original duration.");
 }
 void AddExtra(PartModule module,string name,string title,string unit,float min,float max,float step,string help){var f=module.Fields[name];if(f==null)return;float v=Convert.ToSingle(f.GetValue(module));var s=new Setting{Name=name,Label=title,Unit=unit,Help=help,Front=false,Value=v,Original=v,Min=min,Max=max,Step=step,Text=Number(v)};s.Targets.Add(new Target{Module=module,Field=f});settings.Add(s);}
 public bool PreviewCarSetting(string name,float value){return PreviewValue(false,name,value);}
 void DrawBrakeTuning(){
  GUI.Label(new Rect(29,316,375,35),"SERVICE BRAKES",sectionStyle);var s=Find(false,"brakePowerPercent");if(s==null)return;
  GUI.BeginGroup(new Rect(24,372,390,120));Row(s,0);GUI.EndGroup();
  GUI.Label(new Rect(29,510,375,165),"Adjusts the normal brake pedal's power on all wheels. In Drift mode, B still brakes only the front axle.\n\nEngine braking, automatic SC/TCS brake torque, the rear handbrake and burnout hold keep their original strength.",label);
  GUI.Label(new Rect(29,711,375,65),"Apply to car, then Save Car. ABS retains control when enabled.",small);
 }
 void BindTireGrip(){
  if(body==null)return;foreach(var p in body.children){var grip=p.FindModuleImplementing<KDAGrip>();if(grip==null)continue;
   bool front=body.transform.InverseTransformPoint(p.transform.position).y>0;float v=grip.gripMultiplier;var s=Find(front,"gripMultiplier");
   if(s==null){s=new Setting{Name="gripMultiplier",Label=front?"Front tire grip":"Rear tire grip",Unit="x",Help="1.00x = original imported tire calibration.",Front=front,Value=v,Original=v,Min=.4f,Max=3,Step=.05f,Text=Number(v)};settings.Add(s);}else if(Mathf.Abs(s.Original-v)>.0001f)s.Mixed=true;
   s.Targets.Add(new Target{Module=grip,Field=grip.Fields["gripMultiplier"]});
  }
 }
 void DrawTireGrip(){
  GUI.BeginGroup(new Rect(24,310,406,474));GUI.Label(new Rect(4,0,375,28),"TIRE GRIP",sectionStyle);
  for(int i=0;i<2;i++){var s=Find(i==0,"gripMultiplier");if(s!=null)Row(s,48+i*104);}
  GUI.Label(new Rect(4,272,375,125),"Adjust grip without changing springs, ride height or the chassis preset. Front and rear can be tuned separately.\n\nChoosing a chassis preset later replaces these values with that preset's grip. Apply, then Save Car to keep your setup.",small);
  if(Button(new Rect(4,412,370,36),"RESET BOTH AXLES TO 1.00x",false))foreach(bool front in new[]{true,false}){var s=Find(front,"gripMultiplier");if(s!=null)Set(s,1,true);}
  GUI.EndGroup();
 }
}
