// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using UnityEngine;
using KerbalDriftAgency;

// Offline-generated, shared AudioClips + bounded native Unity mixing. No custom
// audio-thread callback, per-car synthesis, per-frame arrays or waveform copies.
public sealed class KDAHybridAudio:MonoBehaviour {
 const string Root="KDAVehicles/Sounds/Hybrid/";
 public const int MaximumAudibleCars=4;
 static readonly List<KDAHybridAudio> clients=new List<KDAHybridAudio>();
 static readonly Dictionary<string,AudioClip[]> cache=new Dictionary<string,AudioClip[]>();
 static float nextBudget;static Transform listener;
 Part part;KDAEngine engine;GameObject voiceObject,effectsObject;AudioLowPassFilter filter;
 readonly AudioSource[] voices=new AudioSource[12];readonly bool[] scheduled=new bool[12];readonly int[] loadedBand={-1,-1};
 AudioSource intake,turbo,bov,pop;AudioClip popClip;AudioClip[] clips;
 readonly HybridAudioEnvelope envelope=new HybridAudioEnvelope();
 readonly BoostReleaseTrigger release=new BoostReleaseTrigger();
 string family="";string layout="";double liters=2;int selected=-1,shiftSerial;bool audible,playing,failed,ready;
 readonly float[] levels={1,1,1,1,.55f,1,.70f}, desired={1,1,1,1,.55f,1,.70f};
 static readonly string[] StemNames={"body","pulses","texture"};
 public void SetMix(float body,float pulses,float texture,float intakeGain,float bovGain,float shiftGain,float idleGain){
  desired[0]=(float)HybridAudioCore.Clean(body,1,0,2);desired[1]=(float)HybridAudioCore.Clean(pulses,1,0,3);
  desired[2]=(float)HybridAudioCore.Clean(texture,1,0,3);desired[3]=(float)HybridAudioCore.Clean(intakeGain,1,0,3);
  desired[4]=(float)HybridAudioCore.Clean(bovGain,.55,0,1);desired[5]=(float)HybridAudioCore.Clean(shiftGain,1,0,2);
  desired[6]=(float)HybridAudioCore.Clean(idleGain,.70,0,1);
 }
 float lastTick;double startDsp;int budgetId;
 public int PopEvents {get;private set;}public int BovEvents {get;private set;}
 public string Family {get{return family;}}public bool Playing {get{return playing;}}
 public float Pitch {get{return voices[0]!=null?voices[0].pitch:0;}}
 public static int AudibleCars {get;private set;}
 public void Initialize(Part owner,string sourceLayout,float displacement){
  part=owner;engine=part.FindModuleImplementing<KDAEngine>();layout=sourceLayout??"";
  liters=HybridAudioCore.Displacement(engine==null?"":engine.importedEngineName,displacement);
  ready=false;budgetId=GetInstanceID();lastTick=Time.realtimeSinceStartup;
  voiceObject=new GameObject("KDA hybrid combustion");voiceObject.transform.SetParent(part.transform,false);
  // Unity requires an AudioSource (or listener) before adding an audio filter.
  for(int i=0;i<voices.Length;i++)voices[i]=Source(voiceObject,true,true);
  filter=voiceObject.AddComponent<AudioLowPassFilter>();filter.lowpassResonanceQ=.85f;filter.cutoffFrequency=5000;
  effectsObject=new GameObject("KDA hybrid detail");effectsObject.transform.SetParent(part.transform,false);
  intake=Effect(Root+"intake",true);
  turbo=Effect("KDAVehicles/Sounds/kda_turbo",true);bov=Effect("KDAVehicles/Sounds/kda_bov",false);
  popClip=KDAAfterfire.MakeClip(true);pop=Source(effectsObject,false,false);pop.clip=popClip;
  shiftSerial=engine==null?0:engine.ShiftSerial;
  ready=true;clients.Add(this);nextBudget=0;
 }
 static AudioSource Source(GameObject obj,bool loop,bool combustion){
  var source=obj.AddComponent<AudioSource>();source.loop=loop;source.playOnAwake=false;source.volume=0;
  KDAAudioMix.Configure(source,combustion);source.dopplerLevel=.15f;source.priority=combustion?96:128;
  return source;
 }
 AudioSource Effect(string path,bool loop){var source=Source(effectsObject,loop,false);source.clip=GameDatabase.Instance.GetAudioClip(path);return source;}
 bool Configure(int choice){
  if(choice==selected)return !failed;selected=choice;Stop();failed=false;
  family=HybridAudioCore.Resolve(layout,engine==null?"":engine.importedEngineName,choice);
  if(!cache.TryGetValue(family,out clips)||clips==null||clips.Length!=48||clips[0]==null){
   clips=new AudioClip[48];for(int i=0;i<8;i++)for(int bank=0;bank<2;bank++)for(int stem=0;stem<3;stem++)clips[(i*2+bank)*3+stem]=GameDatabase.Instance.GetAudioClip(Root+"stems/"+family+"/"+StemNames[stem]+"/rpm_"+i+"_"+bank);
   cache[family]=clips;
  }
  for(int i=0;i<clips.Length;i++)if(clips[i]==null){failed=true;break;}
  if(failed)Debug.LogError("[KDA Hybrid Audio] Missing shared "+family+" bank. Install this branch's complete base GameData; exported recordings are not used as fallback.");
  else Debug.Log("[KDA Hybrid Audio] "+part.name+" voice="+family+"; shared clips; max 12 synchronized tuning loops/car and 4 audible cars. Auto V8 uses cross-plane; boxer4 uses unequal-header preset.");
  return !failed;
 }
 bool Eligible(){return engine!=null&&part!=null&&part.vessel!=null&&!part.vessel.packed&&engine.engineRunning&&HighLogic.LoadedSceneIsFlight;}
 static void Budget(){
  float now=Time.realtimeSinceStartup;if(now<nextBudget)return;nextBudget=now+.25f;
  if(FlightCamera.fetch!=null&&FlightCamera.fetch.mainCamera!=null)listener=FlightCamera.fetch.mainCamera.transform;
  else if(Camera.main!=null)listener=Camera.main.transform;
  AudibleCars=0;foreach(var item in clients)item.audible=false;
  for(int slot=0;slot<MaximumAudibleCars;slot++){
   KDAHybridAudio best=null;double score=double.MaxValue;
   foreach(var item in clients){if(item==null||item.audible||!item.Eligible())continue;
    bool active=item.part.vessel==FlightGlobals.ActiveVessel;
    double distance=listener!=null?(item.part.transform.position-listener.position).sqrMagnitude:active?0:double.MaxValue;
    if(distance>160*160)continue;double rank=distance+(active?-100000:0);
    if(rank<score||(rank==score&&best!=null&&item.budgetId<best.budgetId)){score=rank;best=item;}
   }
   if(best==null)break;best.audible=true;AudibleCars++;
  }
  foreach(var item in clients)if(item!=null&&!item.audible)item.Stop();
 }
 public void Tick(float volume,float fxVolume,int pipe,int voice,float tone){
  if(!ready||engine==null)return;
  volume=(float)HybridAudioCore.Clean(volume,.7,0,1);fxVolume=(float)HybridAudioCore.Clean(fxVolume,.55,0,1);tone=(float)HybridAudioCore.Clean(tone,.55,0,1);
  Budget();float now=Time.realtimeSinceStartup,dt=Mathf.Clamp(now-lastTick,0,.1f);lastTick=now;
  for(int i=0;i<levels.Length;i++)levels[i]=Mathf.Lerp(levels[i],desired[i],1-(float)Math.Exp(-dt/.05));
  bool shifted=engine.ShiftSerial!=shiftSerial;shiftSerial=engine.ShiftSerial;
  bool lift=release.Step(engine.RequestedThrottle,false,engine.HasTurbo,engine.boostBar);
  bool active=Eligible()&&audible&&Time.timeScale>0&&TimeWarp.CurrentRate<=1&&!PauseMenu.isOpen&&!MapView.MapIsEnabled;
  float master=Mathf.Clamp01(GameSettings.SHIP_VOLUME);
  if(!active||master<=0||volume<=0){Stop();return;}
  if(!Configure(Mathf.Clamp(voice,0,HybridAudioCore.Families.Length)))return;
  if(!playing){envelope.Rpm=HybridAudioCore.Clean(engine.engineRpm,900,300,24000);envelope.Load=HybridAudioCore.Clean(engine.AudioThrottle,0,0,1);}
  envelope.Step(dt,engine.engineRpm,engine.AudioThrottle,engine.AtRevLimiter||engine.IsShifting,true);
  float load=(float)envelope.Load,gain=master*Mathf.Clamp01(volume)*(float)envelope.Gain*(levels[6]+(1-levels[6])*(float)HybridAudioCore.IdleBlend(envelope.Rpm,engine.idleRpm,envelope.Load));
  int lo=HybridAudioCore.Band(envelope.Rpm);float blend=(float)HybridAudioCore.Blend(envelope.Rpm,lo);
  // Stop sources for silent banks instead of leaving every shared stem bank playing.
  Layer(lo,1-blend,load,gain,pipe);Layer(lo+1,blend,load,gain,pipe);playing=true;
  float cutoff=(float)HybridAudioCore.ToneCutoff(tone,load,pipe,liters,HybridAudioCore.IdleBlend(envelope.Rpm,engine.idleRpm,envelope.Load));
  filter.cutoffFrequency=Mathf.Lerp(filter.cutoffFrequency,cutoff,1-(float)Math.Exp(-dt/.07));
  filter.lowpassResonanceQ=(float)HybridAudioCore.ExhaustQ(pipe);
  if(intake.clip!=null){intake.pitch=.75f+Mathf.Clamp01(engine.engineRpm/16000f)*.65f;intake.volume=gain*levels[3]*load*(.07f+.08f*Mathf.Clamp01(tone));Loop(intake);}
  if(turbo.clip!=null){
   float boost=Mathf.Clamp01(engine.boostBar/Mathf.Max(.1f,engine.HudMaximumBoost));
   bool blower=engine.inductionSetup==4;
   turbo.pitch=blower?.65f+Mathf.Clamp(engine.engineRpm/6500,.1f,1.7f):.65f+Mathf.Clamp01(engine.TurboRotor)*.95f;
   turbo.volume=master*Mathf.Clamp01(fxVolume)*(blower?(.05f+.15f*load)*Mathf.Clamp01(engine.engineRpm/6500):engine.HasTurbo?boost*.35f:0);Loop(turbo);
  }
  if(bov.clip!=null&&(lift||shifted&&engine.HasTurbo&&engine.boostBar>.08f)){
   bov.volume=master*levels[4];bov.Stop();bov.PlayOneShot(bov.clip,Mathf.Clamp(engine.boostBar/Mathf.Max(.1f,engine.HudMaximumBoost),.1f,.8f));BovEvents++;
  }
  if(shifted&&engine.transmissionSetup==2&&pop.clip!=null){
   pop.volume=KDAAudioMix.ExhaustVolume(master*Mathf.Clamp01(volume)*Mathf.Clamp01(engine.afterfireVolume)*Mathf.Clamp01(engine.afterfireAmount),pipe)*(float)AfterfireSoundCore.TuneGain(engine.afterfireTune)*levels[5];
   if(pop.volume>.001f){pop.Stop();pop.PlayOneShot(pop.clip,.45f);PopEvents++;}
  }
 }
 void Layer(int index,float weight,float load,float gain,int pipe){
  int slot=index%2;bool changed=loadedBand[slot]!=index;
  if(changed){loadedBand[slot]=index;startDsp=AudioSettings.dspTime+.012;}
  for(int bank=0;bank<2;bank++)for(int stem=0;stem<3;stem++){
   int voiceIndex=(slot*2+bank)*3+stem;var source=voices[voiceIndex];var clip=clips[(index*2+bank)*3+stem];
   if(changed){source.Stop();scheduled[voiceIndex]=false;source.clip=clip;}
   source.pitch=(float)HybridAudioCore.Pitch(envelope.Rpm,index,clip.samples,clip.frequency);
   source.volume=KDAAudioMix.ExhaustVolume(gain*weight*(bank==1?load:1-load)*levels[stem],pipe);
   if(source.volume<.0005f){if(scheduled[voiceIndex]||source.isPlaying)source.Stop();scheduled[voiceIndex]=false;continue;}
   if(!scheduled[voiceIndex]&&!source.isPlaying){
    double begin=changed?startDsp:AudioSettings.dspTime+.012;
    double cycles=envelope.CrankCycles+envelope.Rpm*.012/120;
    source.timeSamples=Math.Min(clip.samples-1,(int)(HybridAudioCore.Phase(cycles,index)*clip.samples));
    source.PlayScheduled(begin);scheduled[voiceIndex]=true;
   }
  }
 }
 static void Loop(AudioSource source){if(source.volume>.0005f){if(!source.isPlaying)source.Play();}else if(source.isPlaying)source.Stop();}
 void Stop(){
  if(!playing&&loadedBand[0]<0&&loadedBand[1]<0)return;
  for(int i=0;i<voices.Length;i++)if(voices[i]!=null){voices[i].Stop();voices[i].volume=0;scheduled[i]=false;}
  foreach(var source in new[]{intake,turbo,bov,pop})if(source!=null){source.Stop();source.volume=0;}
  loadedBand[0]=loadedBand[1]=-1;playing=false;envelope.Gain=0;
 }
 void OnDestroy(){Stop();clients.Remove(this);nextBudget=0;if(voiceObject!=null)Destroy(voiceObject);if(effectsObject!=null)Destroy(effectsObject);if(popClip!=null)Destroy(popClip);}
}
