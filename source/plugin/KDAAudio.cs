// SPDX-License-Identifier: MIT
using System;
using UnityEngine;
// Legacy audio parts share the same bounded hybrid player as imported cars.
public class KDAAudio:PartModule {
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
 [KSPField(isPersistant=true,guiActive=true,guiName="Engine volume",guiFormat="P0")]
 [UI_FloatRange(minValue=0,maxValue=1,stepIncrement=.05f)] public float engineVolume=.65f;
 public int PopEvents {get{return hybrid==null?0:hybrid.PopEvents;}}
 public int BovEvents {get{return hybrid==null?0:hybrid.BovEvents;}}
 public bool LoopsPlaying {get{return hybrid!=null&&hybrid.Playing;}}
 public float EnginePitch {get{return hybrid==null?0:hybrid.Pitch;}}
 public override void OnStart(StartState state){base.OnStart(state);if(!HighLogic.LoadedSceneIsFlight)return;hybrid=gameObject.AddComponent<KDAHybridAudio>();hybrid.Initialize(part,"",0);}
 void Update(){if(hybrid!=null){hybrid.SetMix(bodyLevel,pulseLevel,textureLevel,intakeLevel,bovVolume,shiftLevel,idleLevel);hybrid.Tick(engineVolume,turboVolume,exhaustSound,engineVoice,engineTone);}}
 void OnDestroy(){if(hybrid!=null)Destroy(hybrid);}
 public override string GetInfo(){return "Original KDA hybrid engine audio with automatic voice, tone, load, shift and limiter response. Shared banks; no exported recordings.";}
}

public static class KDAAudioMix {
 public const float EngineGain=.90f;
 // Increase exhaust loudness only, within the AudioSource volume range.
 // Standard returns its original mix unchanged; no clipping/distortion effect.
 public static float ExhaustVolume(float standard,int mode){return Mathf.Clamp01(standard*(float)KerbalDriftAgency.HybridAudioCore.ExhaustGain(mode));}
 public static void Configure(AudioSource source,bool engine){source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Logarithmic;source.minDistance=engine?20:12;source.maxDistance=160;}
 public static bool NormalizePair(AudioClip off,AudioClip on,out AudioClip normalizedOff,out AudioClip normalizedOn){
  normalizedOff=off;normalizedOn=on;if(off==null||on==null)return false;
  float[] a=new float[off.samples*off.channels],b=new float[on.samples*on.channels];
  if(!off.GetData(a,0)||!on.GetData(b,0)){Debug.LogWarning("[KDA Audio] Clip cannot be read for level calibration; using original audio");return false;}
  float pa,ra,pb,rb;Measure(a,out pa,out ra);Measure(b,out pb,out rb);
  float peak=Mathf.Max(pa,pb),rms=Mathf.Max(ra,rb);if(peak<.00001f||rms<.00001f)return false;
  // One gain for each off/on pair retains the recorded load contrast. A peak
  // ceiling leaves room for turbo/effects; silence is never amplified.
  float gain=Mathf.Clamp(Mathf.Min(.24f/rms,.72f/peak),.05f,64);
  if(Mathf.Abs(gain-1)<.01f)return false;
  for(int i=0;i<a.Length;i++)a[i]*=gain;for(int i=0;i<b.Length;i++)b[i]*=gain;
  normalizedOff=AudioClip.Create(off.name+" KDA level",off.samples,off.channels,off.frequency,false);normalizedOff.SetData(a,0);
  normalizedOn=AudioClip.Create(on.name+" KDA level",on.samples,on.channels,on.frequency,false);normalizedOn.SetData(b,0);return true;
 }
 static void Measure(float[] samples,out float peak,out float rms){peak=0;double energy=0;foreach(float sample in samples){peak=Mathf.Max(peak,Mathf.Abs(sample));energy+=(double)sample*sample;}rms=(float)Math.Sqrt(energy/Math.Max(1,samples.Length));}
}
