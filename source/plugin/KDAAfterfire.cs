using System;using UnityEngine;using KerbalDriftAgency;

public partial class KDAEngine {
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Service brake power (%)",guiFormat="F0")]
 [UI_FloatRange(minValue=0,maxValue=200,stepIncrement=5)] public float brakePowerPercent=100;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Afterfire tune")]
 [UI_ChooseOption(options=new[]{"Stock","Crackle","Flame"})] public int afterfireTune=0;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Pops / bangs multiplier",guiFormat="F2")]
 [UI_FloatRange(minValue=0,maxValue=3,stepIncrement=.1f)] public float afterfireAmount=1;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Pops / bangs volume",guiFormat="F2")]
 [UI_FloatRange(minValue=0,maxValue=1,stepIncrement=.05f)] public float afterfireVolume=.65f;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Flame size multiplier",guiFormat="F2")]
 [UI_FloatRange(minValue=.25f,maxValue=3,stepIncrement=.05f)] public float flameSize=1;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Flame duration multiplier",guiFormat="F2")]
 [UI_FloatRange(minValue=.5f,maxValue=3,stepIncrement=.1f)] public float flameDuration=1;
 KDAAfterfire afterfire;
 public KDAAfterfire Afterfire {get{if(afterfire==null&&HighLogic.LoadedSceneIsFlight){afterfire=part.gameObject.AddComponent<KDAAfterfire>();afterfire.Engine=this;}return afterfire;}}
}

// One event stream per engine drives every outlet and its matching sound.
// No fuel, torque, throttle or engine braking changes are made by these tunes.
public sealed class KDAAfterfire:MonoBehaviour {
 public KDAEngine Engine;public int Serial {get;private set;}public bool Bang {get;private set;}public int SoundEvents {get;private set;}
 readonly ExhaustCore core=new ExhaustCore();readonly FlamePlumeCore plume=new FlamePlumeCore();System.Random random=new System.Random(48217);AudioClip[] popClips,bangClips;AudioSource source;bool paused;
 public float FlamePlume {get;private set;}
 public bool HasSoundClips {get{return popClips!=null&&bangClips!=null;}}
 void EnsureSound(){if(source!=null)return;popClips=new AudioClip[3];bangClips=new AudioClip[3];for(int i=0;i<3;i++){popClips[i]=MakeClip(false,i);bangClips[i]=MakeClip(true,i);}source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.volume=0;KDAAudioMix.Configure(source,false);source.dopplerLevel=.1f;}
 public static AudioClip MakeClip(bool bang){
  return MakeClip(bang,0);
 }
 static AudioClip MakeClip(bool bang,int variation){var data=AfterfireSoundCore.Samples(bang,variation);var clip=AudioClip.Create((bang?"KDA exhaust bang ":"KDA exhaust pop ")+variation,data.Length,1,AfterfireSoundCore.Rate,false);clip.SetData(data,0);return clip;}
 void Update(){
  if(Engine==null||Engine.vessel==null||Engine.part.packed){FlamePlume=0;if(source!=null)source.Stop();return;}
  if(FlightDriver.Pause||Time.timeScale==0){if(source!=null&&!paused){source.Pause();paused=true;}return;}
  if(paused){if(source!=null)source.UnPause();paused=false;}
  if(TimeWarp.CurrentRate>1){FlamePlume=0;if(source!=null)source.Stop();return;}
  FlamePlume=(float)plume.Step(Time.deltaTime,Engine.engineRunning,Engine.afterfireTune,Engine.RequestedThrottle,Engine.engineRpm,Engine.IsShifting,Engine.afterfireAmount,Engine.flameDuration);
  bool fire=core.Step(Time.deltaTime,Engine.engineRunning,Engine.RequestedThrottle,Engine.engineRpm,Engine.ShiftSerial,Engine.selectedGear,Engine.inductionSetup==3,Engine.boostBar,random.NextDouble(),Engine.afterfireAmount,Engine.afterfireTune);
  if(!Engine.engineRunning){if(source!=null)source.Stop();return;}
  if(!fire)return;Serial++;Bang=random.NextDouble()<(Engine.afterfireTune==2?.03:Engine.afterfireTune==1?.18:.4);EnsureSound();
  var sample=Engine.part.FindModuleImplementing<KDASampleAudio>();var audio=Engine.part.FindModuleImplementing<KDAAudio>();float volume=sample!=null?sample.volume:audio!=null?audio.engineVolume:1;int pipe=sample!=null?sample.exhaustSound:audio!=null?audio.exhaustSound:0;
  source.volume=KDAAudioMix.ExhaustVolume(Mathf.Clamp01(GameSettings.SHIP_VOLUME)*Mathf.Clamp01(volume)*Mathf.Clamp01(Engine.afterfireVolume),pipe)*(float)AfterfireSoundCore.TuneGain(Engine.afterfireTune);source.pitch=.96f+(float)random.NextDouble()*.08f;
  if(source.volume>0){int variant=random.Next(3);source.PlayOneShot(Bang?bangClips[variant]:popClips[variant],Bang?.8f:.65f);SoundEvents++;}
 }
 void OnDestroy(){if(source!=null){source.Stop();Destroy(source);}if(popClips!=null)foreach(var clip in popClips)if(clip!=null)Destroy(clip);if(bangClips!=null)foreach(var clip in bangClips)if(clip!=null)Destroy(clip);}
}
