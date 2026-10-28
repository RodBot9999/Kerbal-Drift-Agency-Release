// SPDX-License-Identifier: MIT
using UnityEngine;
// Retains saved module/field names; exported recordings are no longer played.
public class KDASampleAudio:PartModule {
 [KSPField(isPersistant=true)] public int exhaustSound=0;
 [KSPField(isPersistant=true)] public int engineVoice=0;
 [KSPField(isPersistant=true)] public float engineTone=.55f;
 [KSPField(isPersistant=true)] public float turboVolume=.55f;
 [KSPField(isPersistant=true)] public float bodyLevel=1f;
 [KSPField(isPersistant=true)] public float pulseLevel=1f;
 [KSPField(isPersistant=true)] public float textureLevel=1f;
 [KSPField(isPersistant=true)] public float intakeLevel=1f;
 [KSPField(isPersistant=true)] public float bovVolume=0.55f;
 [KSPField(isPersistant=true)] public float shiftLevel=1f;
 [KSPField(isPersistant=true)] public float idleLevel=0.7f;
 KDAHybridAudio hybrid;
 [KSPField] public string audioRoot="",sampleRpms="",voiceLayout="";
 [KSPField] public float voiceDisplacement=0;
 [KSPField(isPersistant=true,guiActive=true,guiName="Engine volume",guiFormat="P0")]
 [UI_FloatRange(minValue=0,maxValue=1,stepIncrement=.05f)] public float volume=.7f;
 public int NormalizedClipPairs {get{return 0;}}
 public override void OnStart(StartState state){
  base.OnStart(state);if(!HighLogic.LoadedSceneIsFlight)return;
  hybrid=gameObject.AddComponent<KDAHybridAudio>();hybrid.Initialize(part,voiceLayout,voiceDisplacement);
 }
 void Update(){if(hybrid!=null){hybrid.SetMix(bodyLevel,pulseLevel,textureLevel,intakeLevel,bovVolume,shiftLevel,idleLevel);hybrid.Tick(volume,turboVolume,exhaustSound,engineVoice,engineTone);}}
 void OnDestroy(){if(hybrid!=null)Destroy(hybrid);}
 public override string GetInfo(){return "Original KDA hybrid audio: automatic engine voice, shared loops and live intake/boost details. Exported recordings are not played.";}
}
