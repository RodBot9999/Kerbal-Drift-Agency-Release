// SPDX-License-Identifier: MIT
// Narrow behavior doubles. Not compiled into KDAVehicles.dll.
using System;using System.Collections.Generic;
namespace UnityEngine {
 public class Object {static int serial;readonly int id=++serial;public int GetInstanceID(){return id;}public static void Destroy(Object value){}}
 public class MonoBehaviour:Object {public GameObject gameObject;public Transform transform {get{return gameObject.transform;}}}
 public class GameObject:Object {public static int MissingAudioFilterDependencies;public static bool RejectFilters;bool hasAudioSource;public string name;public Transform transform=new Transform();public GameObject(string name){this.name=name;}public T AddComponent<T>() where T:MonoBehaviour,new(){if(typeof(T)==typeof(AudioLowPassFilter)&&(!hasAudioSource||RejectFilters)){MissingAudioFilterDependencies++;return null;}if(typeof(T)==typeof(AudioSource))hasAudioSource=true;return new T{gameObject=this};}}
 public class Transform {public Vector3 position;public void SetParent(Transform p,bool local){}}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 operator-(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}public float sqrMagnitude {get{return x*x+y*y+z*z;}}}
 public static class Mathf {public static float Clamp(float v,float a,float b){return Math.Max(a,Math.Min(b,v));}public static int Clamp(int v,int a,int b){return Math.Max(a,Math.Min(b,v));}public static float Clamp01(float v){return Clamp(v,0,1);}public static float Lerp(float a,float b,float t){return a+(b-a)*Clamp01(t);}public static float Max(float a,float b){return Math.Max(a,b);}}
 public static class Time {public static float realtimeSinceStartup,timeScale=1;}
 public static class AudioSettings {public static double dspTime;}
 public static class Debug {public static void Log(string s){}public static void LogError(string s){throw new Exception(s);}}
 public class Camera:MonoBehaviour {public static Camera main;}
 public class AudioLowPassFilter:MonoBehaviour {public float lowpassResonanceQ,cutoffFrequency;}
 public class AudioClip:Object {public static int Created;public int samples=22050,frequency=22050;public AudioClip(){Created++;}}
 public class AudioSource:MonoBehaviour {
  public static readonly List<AudioSource> All=new List<AudioSource>();public AudioSource(){All.Add(this);}
  public AudioClip clip;public bool loop,playOnAwake;public float volume,pitch,dopplerLevel;public int priority,timeSamples;double at;bool requested;
  public bool isPlaying {get{return requested&&AudioSettings.dspTime>=at;}}
  public void Play(){requested=true;at=AudioSettings.dspTime;}public void PlayScheduled(double when){requested=true;at=when;}
  public void Stop(){requested=false;}public void PlayOneShot(AudioClip value,float volume){}
  public static int PlayingCombustion {get{int n=0;foreach(var s in All)if(s.gameObject.name=="KDA hybrid combustion"&&s.isPlaying)n++;return n;}}
  public static int PlayingTotal {get{int n=0;foreach(var s in All)if(s.isPlaying)n++;return n;}}
 }
}
public class Part {public UnityEngine.GameObject gameObject=new UnityEngine.GameObject("part");public UnityEngine.Transform transform {get{return gameObject.transform;}}public string name="car";public Vessel vessel;public KDAEngine engine;public T FindModuleImplementing<T>() where T:class{return engine as T;}}
public class Vessel {public bool packed;}
public class KDAEngine {public Part part;public bool engineRunning,AtRevLimiter,IsShifting,HasTurbo;public int ShiftSerial,transmissionSetup,inductionSetup;public float engineRpm,idleRpm=900,AudioThrottle,RequestedThrottle,boostBar,HudMaximumBoost=1,TurboRotor,afterfireVolume,afterfireAmount;public int afterfireTune;public string importedEngineName;}
public static class KDAAfterfire {public static UnityEngine.AudioClip MakeClip(bool bang){return new UnityEngine.AudioClip();}}
public static class KDAAudioMix {public static void Configure(UnityEngine.AudioSource source,bool engine){}public static float ExhaustVolume(float value,int pipe){return value*(pipe==1?1.4f:1);}}
public class GameDatabase {public static GameDatabase Instance=new GameDatabase();readonly Dictionary<string,UnityEngine.AudioClip> clips=new Dictionary<string,UnityEngine.AudioClip>();readonly Dictionary<string,int> requests=new Dictionary<string,int>();public int RequestCount(string key){return requests.ContainsKey(key)?requests[key]:0;}public UnityEngine.AudioClip GetAudioClip(string path){requests[path]=RequestCount(path)+1;UnityEngine.AudioClip clip;if(!clips.TryGetValue(path,out clip)){clip=new UnityEngine.AudioClip();clips[path]=clip;}return clip;}}
public class FlightCamera {public static FlightCamera fetch=new FlightCamera();public UnityEngine.Camera mainCamera=new UnityEngine.GameObject("camera").AddComponent<UnityEngine.Camera>();}
public static class FlightGlobals {public static Vessel ActiveVessel;}
public static class HighLogic {public static bool LoadedSceneIsFlight=true;}
public static class TimeWarp {public static float CurrentRate=1;}
public static class PauseMenu {public static bool isOpen;}
public static class MapView {public static bool MapIsEnabled;}
public static class GameSettings {public static float SHIP_VOLUME=1;}
namespace KerbalDriftAgency {public class BoostReleaseTrigger {double previous;public bool Step(double value,bool shifting,bool turbo,double pressure){bool result=previous>.15&&value<=.15;previous=value;return turbo&&pressure>.08&&result;}}public static class AfterfireSoundCore {public static double TuneGain(int tune){return 1;}}}
