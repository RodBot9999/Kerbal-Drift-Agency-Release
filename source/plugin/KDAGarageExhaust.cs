using UnityEngine;

public sealed partial class KDAGarage {
 static readonly int[] ExhaustOrder={2,0,4,3,5,1};
 Target exhaustTarget;int exhaustOriginal,exhaustDraft;
 public int ExhaustDraft {get{return exhaustDraft;}}
 bool ExhaustDirty {get{return exhaustTarget!=null&&exhaustOriginal!=exhaustDraft;}}
 void BindExhaust(){
  exhaustTarget=null;exhaustOriginal=exhaustDraft=0;if(body==null)return;
  PartModule audio=body.FindModuleImplementing<KDASampleAudio>();if(audio==null)audio=body.FindModuleImplementing<KDAAudio>();
  if(audio==null||audio.Fields["exhaustSound"]==null)return;
  exhaustTarget=new Target{Module=audio,Field=audio.Fields["exhaustSound"]};exhaustOriginal=exhaustDraft=System.Convert.ToInt32(exhaustTarget.Field.GetValue(audio));
  AddExtra(audio,audio is KDASampleAudio?"volume":"engineVolume","Engine volume","",0,1,.05f,"Local engine volume; respects the KSP ship volume setting.");
 }
 public bool PreviewExhaust(int mode){if(!open||loadingCar||exhaustTarget==null||mode<0||mode>=KerbalDriftAgency.HybridAudioCore.ExhaustLabels.Length)return false;exhaustDraft=mode;return true;}
 void DrawExhaust(){
  scroll=GUI.BeginScrollView(new Rect(24,310,406,474),scroll,new Rect(0,0,380,1090));
  GUI.Label(new Rect(4,0,370,30),"ENGINE SOUND",sectionStyle);
  var volume=Find(false,"volume")??Find(false,"engineVolume");if(volume!=null)Row(volume,42);
  GUI.Label(new Rect(4,146,370,30),"EXHAUST",sectionStyle);
  bool old=GUI.enabled;GUI.enabled=old&&exhaustTarget!=null;
  for(int i=0;i<ExhaustOrder.Length;i++){int mode=ExhaustOrder[i];if(Button(new Rect(4+(i%2)*190,184+(i/2)*43,180,36),KerbalDriftAgency.HybridAudioCore.ExhaustLabels[mode],exhaustDraft==mode))PreviewExhaust(mode);}GUI.enabled=old;
  GUI.Label(new Rect(4,318,370,60),exhaustTarget==null?"Engine audio module unavailable.":KerbalDriftAgency.HybridAudioCore.ExhaustHelp[KerbalDriftAgency.HybridAudioCore.ExhaustIndex(exhaustDraft)]+" Sound only.",small);
  GUI.Label(new Rect(4,386,370,60),"Engine character follows the imported engine. Apply, then Save Car to keep volume and exhaust settings.",small);
  GUI.Label(new Rect(4,474,370,29),"POPS, BANGS & FLAMES",sectionStyle);var tune=Find(false,"afterfireTune");
  if(tune!=null){string[] modes={"STOCK","CRACKLE","FLAME"};for(int i=0;i<3;i++)if(Button(new Rect(4+i*126,512,118,36),modes[i],(int)tune.Value==i))Set(tune,i,true);}
  string[] fields={"afterfireAmount","afterfireVolume","flameSize","flameDuration"};for(int i=0;i<fields.Length;i++){var setting=Find(false,fields[i]);if(setting!=null)Row(setting,567+i*92);}
  GUI.Label(new Rect(4,964,370,67),"Exhaust sound does not change power, fuel or handling. Flame-kit controls are separate. Apply, then Save Car.",small);GUI.EndScrollView();
 }
}
