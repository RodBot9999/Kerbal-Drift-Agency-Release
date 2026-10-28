using System;
using System.Reflection;
using ModuleWheels;
using UnityEngine;
using KerbalDriftAgency;

[DefaultExecutionOrder(1250)]
public class KDATireSmoke : PartModule {
 ModuleWheelBase wheelBase;KDAEngine engine;ParticleSystem smoke;Material material;Texture2D texture;
 const int ParticleBudget=1000;
 readonly ParticleSystem.Particle[] cloud=new ParticleSystem.Particle[ParticleBudget];
 readonly Vector3[] floors=new Vector3[ParticleBudget],normals=new Vector3[ParticleBudget];
 readonly bool[] burnoutPuff=new bool[ParticleBudget];
 readonly bool[] nearWheelPuff=new bool[ParticleBudget];
 readonly Vector3[] puffBack=new Vector3[ParticleBudget];
 readonly float[] rearwardSpeed=new float[ParticleBudget];
 readonly float[] puffHeat=new float[ParticleBudget];
 readonly TireHeatCore heat=new TireHeatCore();readonly TireSmokeEnvelope envelope=new TireSmokeEnvelope();
 Vector3 contact,normal=Vector3.up;float freshContact=-100,emissionCredit;uint serial;int emitted;
 bool contactValid,previousContactValid;Vector3 wind,previousContact;float nextWind,environmentLight=1;bool shifting;
 Color environmentTint=Color.white;
 [KSPField(guiActive=true,guiName="Smoke tire temp",guiUnits=" C",guiFormat="F0")] public float smokeTireTemperature=25;
 public int ParticleCount {get{return smoke==null?0:smoke.particleCount;}}
 public float Strength {get{return (float)envelope.Strength;}}
 public int EmittedCount {get{return emitted;}}
 public bool GroundedEmitter {get{return contactValid&&wheelBase!=null&&wheelBase.Wheel!=null&&wheelBase.Wheel.IsGrounded&&!part.packed&&!FlightDriver.Pause&&Time.time-freshContact<.15f;}}
 public override void OnStart(StartState state){
  base.OnStart(state);if(!HighLogic.LoadedSceneIsFlight)return;
  wheelBase=part.FindModuleImplementing<ModuleWheelBase>();
  var shader=Shader.Find("Particles/Alpha Blended")??Shader.Find("Legacy Shaders/Particles/Alpha Blended");
  if(shader==null){Debug.LogError("[KDA] Smoke shader unavailable");return;}
  texture=new Texture2D(128,128,TextureFormat.RGBA32,false);texture.wrapMode=TextureWrapMode.Clamp;
  var pixels=new Color[128*128];for(int y=0;y<128;y++)for(int x=0;x<128;x++){
   float u=(x-63.5f)/63.5f,v=(y-63.5f)/63.5f,r=Mathf.Sqrt(u*u+v*v);
   float noise=.55f*Mathf.PerlinNoise(x*.045f,y*.045f)+.3f*Mathf.PerlinNoise(x*.11f+5,y*.11f)+.15f*Mathf.PerlinNoise(x*.23f,y*.23f+9);
   float edge=Mathf.SmoothStep(0,1,Mathf.Clamp01((1-r)*2.2f));
   // Subtle internal shading keeps a dense puff from reading as flat white.
   // Its alpha silhouette and density are deliberately unchanged.
   float shade=.88f+.12f*noise;
   pixels[y*128+x]=new Color(shade,shade,shade,edge*(.28f+.72f*noise));
  }texture.SetPixels(pixels);texture.Apply(false,true);
  material=new Material(shader);material.mainTexture=texture;
  var obj=new GameObject("KDA grounded tire smoke");obj.transform.SetParent(part.transform,false);
  smoke=obj.AddComponent<ParticleSystem>();smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
  var main=smoke.main;main.loop=true;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
  main.maxParticles=ParticleBudget;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;main.startSpeed=0;main.startRotation=new ParticleSystem.MinMaxCurve(0,6.28f);
  var emission=smoke.emission;emission.enabled=false;var shape=smoke.shape;shape.enabled=false;
  var size=smoke.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.65f),new Keyframe(.2f,1.3f),new Keyframe(.6f,2.2f),new Keyframe(1,3.1f)));
  var fade=smoke.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
  gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.08f),new GradientAlphaKey(.7f,.65f),new GradientAlphaKey(0,1)});fade.color=new ParticleSystem.MinMaxGradient(gradient);
  var renderer=obj.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;
  renderer.sortMode=ParticleSystemSortMode.Distance;renderer.maxParticleSize=.35f;
  GameEvents.onFloatingOriginShift.Add(OriginShift);GroundFXSmokeBridge.Discover();smoke.Play();
 }
 void FixedUpdate(){
  if(smoke==null||wheelBase==null||wheelBase.Wheel==null||vessel==null)return;
  var wheel=wheelBase.Wheel;var state=wheel.currentState;
  bool grounded=!part.packed&&!FlightDriver.Pause&&wheel.IsGrounded&&state.hit.collider!=null&&state.hit.normal.sqrMagnitude>.5f;
  if(grounded){
   // Reject stale far-away contacts during packing, jumps and destruction.
   float reach=wheel.WheelRadius+wheel.wheelCollider.suspensionDistance+.5f;
   grounded=(state.hit.point-wheel.WheelCenter).sqrMagnitude<reach*reach;
  }
  contactValid=grounded;
  if(grounded){contact=state.hit.point;normal=state.hit.normal.normalized;freshContact=Time.time;}
  envelope.Step(Time.fixedDeltaTime,grounded,state.angularVelocity*wheel.WheelRadius,state.localWheelVelocity.y,state.localWheelVelocity.x);
  heat.Step(Time.fixedDeltaTime,grounded?TireSmokeCore.Strength(true,Math.Abs(state.angularVelocity*wheel.WheelRadius),Math.Abs(state.localWheelVelocity.y),state.localWheelVelocity.x):0);
  smokeTireTemperature=(float)heat.Temperature;
 }
 void Update(){
  if(smoke==null||vessel==null)return;
  if(part.packed){ClearCloud();return;}
  if(FlightDriver.Pause)return;
  if(engine==null||engine.vessel!=vessel){engine=null;foreach(Part p in vessel.parts){engine=p.FindModuleImplementing<KDAEngine>();if(engine!=null)break;}}
  var settings=engine==null?null:engine.DrivingEffects;
  if(!GroundedEmitter||settings==null||!settings.smokeEnabled){emissionCredit=0;previousContactValid=false;return;}
  smoke.transform.position=contact;
  if(Time.unscaledTime>=nextWind){
   nextWind=Time.unscaledTime+.25f;wind=GroundFXSmokeBridge.Wind(vessel.mainBody,contact);
   // The legacy particle shader is unlit. Match the local day/night cycle
   // instead of leaving bright white tire clouds glowing in night footage.
   if(FlightGlobals.Bodies.Count>0){
    Vector3 sun=(Vector3)(FlightGlobals.Bodies[0].position-(Vector3d)contact).normalized;
    Vector3 up=(Vector3)((Vector3d)contact-vessel.mainBody.position).normalized;
    environmentLight=Mathf.Lerp(.16f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.08f,.22f,Vector3.Dot(sun,up))));
    Color sunlight=Sun.Instance!=null&&Sun.Instance.sunLight!=null?Sun.Instance.sunLight.color:Color.white;
    float peak=Mathf.Max(.001f,Mathf.Max(sunlight.r,Mathf.Max(sunlight.g,sunlight.b)));
    sunlight=new Color(Mathf.Clamp(sunlight.r/peak,.45f,1),Mathf.Clamp(sunlight.g/peak,.45f,1),Mathf.Clamp(sunlight.b/peak,.45f,1));
    environmentTint=Color.Lerp(new Color(.78f,.84f,1),sunlight,Mathf.InverseLerp(.16f,1,environmentLight));
   }
  }
  // Bounded per-wheel budget; density increases opacity rather than allocating
  // thousands of overlapping particles. Never inherit the car's upward speed.
  float intensity=Strength*Mathf.Clamp01(settings.smokeDensity);
  float rate=intensity*Mathf.Lerp(40,110,(float)envelope.TrailBuild);
  emissionCredit+=rate*Mathf.Min(.05f,Time.deltaTime);
  int count=Mathf.Min(12,Mathf.FloorToInt(emissionCredit));emissionCredit-=count;
  Vector3 back=engine==null?Vector3.ProjectOnPlane(wheelBase.Wheel.wheelCollider.cachedTransform.forward,normal):-Vector3.ProjectOnPlane(engine.part.transform.up,normal);
  if(engine!=null&&engine.driveSelector<0)back=-back;
  if(back.sqrMagnitude<.1f)back=Vector3.Cross(normal,Vector3.right);back.Normalize();
  Vector3 right=Vector3.Cross(normal,back).normalized;
  bool burnoutCloud=envelope.Burnout||envelope.RollingBurnout;
  Vector3 roadVelocity=Vector3.ProjectOnPlane((Vector3)vessel.srf_velocity,normal);
  for(int i=0;i<count;i++){
   uint seed=++serial;int slot=(int)(seed%ParticleBudget);
   // Fill the travelled contact segment at low frame rates without ever
   // bridging an airborne gap or interpolating through a large teleport.
   Vector3 birth=previousContactValid&&(contact-previousContact).sqrMagnitude<144?Vector3.Lerp(previousContact,contact,(i+1f)/count):contact;
   floors[slot]=birth;normals[slot]=normal;burnoutPuff[slot]=burnoutCloud;
   // Spread the existing density between a low, lingering wheel cloud and
   // the hotter plume. This does not add a second emitter or more particles.
   bool nearWheel=burnoutCloud&&seed%5<2;nearWheelPuff[slot]=nearWheel;puffBack[slot]=back;
   float lift=burnoutCloud?(float)GroundSmokeMotion.HeatLift(heat.Temperature):0;puffHeat[slot]=lift;
   rearwardSpeed[slot]=burnoutCloud?(float)GroundSmokeMotion.RearwardSpeed(roadVelocity.magnitude,nearWheel)*UnityEngine.Random.Range(.75f,1.25f):0;
   var puff=new ParticleSystem.EmitParams();puff.randomSeed=seed;puff.position=birth+normal*.08f+right*UnityEngine.Random.Range(-.09f,.09f);
   Vector3 carry=nearWheel&&roadVelocity.sqrMagnitude>.01f?roadVelocity.normalized*(float)GroundSmokeMotion.WakeCarry(roadVelocity.magnitude):Vector3.zero;
   puff.velocity=burnoutCloud?back*rearwardSpeed[slot]+carry+right*UnityEngine.Random.Range(-.65f,.65f)+normal*(nearWheel?.045f:.1f):back*UnityEngine.Random.Range(.2f,.7f)+right*UnityEngine.Random.Range(-.35f,.35f)+normal*.015f;
   puff.startLifetime=burnoutCloud?(nearWheel?UnityEngine.Random.Range(4.2f,5.8f):UnityEngine.Random.Range(4.5f+2*lift,6+2*lift)):UnityEngine.Random.Range(3.2f,4.5f);
   puff.startSize=UnityEngine.Random.Range(.45f,.8f);puff.rotation=UnityEngine.Random.Range(0,360);
   float light=GroundFXSmokeBridge.Brightness*environmentLight;
   puff.startColor=new Color(.82f*light*environmentTint.r,.84f*light*environmentTint.g,.86f*light*environmentTint.b,Mathf.Lerp(.2f,.57f,intensity)*Mathf.Min(1.35f,(float)heat.SmokeMultiplier));
   smoke.Emit(puff,1);emitted++;
  }
  previousContact=contact;previousContactValid=true;
 }
 void LateUpdate(){
  if(smoke==null||part.packed||FlightDriver.Pause)return;
  int count=smoke.GetParticles(cloud);float dt=Mathf.Min(.05f,Time.deltaTime);
  for(int i=0;i<count;i++){
   var p=cloud[i];int slot=(int)(p.randomSeed%ParticleBudget);Vector3 n=normals[slot];if(n.sqrMagnitude<.5f)continue;
   float height=Vector3.Dot(p.position-floors[slot],n);float vertical=Vector3.Dot(p.velocity,n);
   Vector3 horizontal=Vector3.ProjectOnPlane(p.velocity,n),air=Vector3.ProjectOnPlane(wind,n);
   float ambientSpeed=air.magnitude;
   air=ambientSpeed>.001f?air*((float)GroundSmokeMotion.TireWindSpeed(ambientSpeed)/ambientSpeed):Vector3.zero;
   // A short, deterministic sideways roll breaks up strings of identical
   // billboards. It adds no lift and never follows an airborne car.
   float age=p.startLifetime-p.remainingLifetime;
   if(age<1.5f){
    Vector3 tangent=Vector3.Cross(n,Mathf.Abs(n.y)<.9f?Vector3.up:Vector3.right).normalized;
    float phase=(p.randomSeed%251)*.173f+age*2.2f;
    horizontal+=(tangent*Mathf.Sin(phase)+Vector3.Cross(n,tangent)*Mathf.Cos(phase))*(dt*.32f*(1-age/1.5f));
   }
   Vector3 wake=burnoutPuff[slot]?puffBack[slot]*(float)GroundSmokeMotion.RearwardTarget(rearwardSpeed[slot],age,nearWheelPuff[slot]):Vector3.zero;
   horizontal=Vector3.Lerp(horizontal,air+wake,1-Mathf.Exp(-dt*(nearWheelPuff[slot]?.8f:.4f)));
   vertical=(float)GroundSmokeMotion.Vertical(height,vertical,age,burnoutPuff[slot],dt,puffHeat[slot],nearWheelPuff[slot]);
   if(height<.065f)p.position+=n*(.065f-height);
   p.velocity=horizontal+n*vertical;cloud[i]=p;
  }
  smoke.SetParticles(cloud,count);
 }
 void ClearCloud(){smoke.Clear();contactValid=false;previousContactValid=false;freshContact=-100;emissionCredit=0;envelope.Step(0,false,0,0,0);}
 void OriginShift(Vector3d offset,Vector3d terrainOffset){
  if(smoke==null||shifting)return;shifting=true;
  try {
   // This system is deliberately not registered with KSP's separate particle
   // offset list: shift both particles and their birth planes exactly once.
   Vector3 delta=(Vector3)terrainOffset;int count=smoke.GetParticles(cloud);
   for(int i=0;i<count;i++){var p=cloud[i];p.position-=delta;cloud[i]=p;}
   smoke.SetParticles(cloud,count);for(int i=0;i<ParticleBudget;i++)floors[i]-=delta;contact-=delta;previousContact-=delta;
  }finally{shifting=false;}
 }
 void OnDestroy(){GameEvents.onFloatingOriginShift.Remove(OriginShift);if(smoke!=null)Destroy(smoke.gameObject);if(material!=null)Destroy(material);if(texture!=null)Destroy(texture);}
}

// Optional, read-only integration with VGFX (formerly GroundBlastFx). No DLL,
// shaders or assets are redistributed, and its rocket renderer is never patched.
public static class GroundFXSmokeBridge {
 static bool tried,failed;static Func<CelestialBody,Vector3,Vector3> sampleWind;
 static object settings;static FieldInfo brightness;static float nextSettings,light=1;
 public static bool Available {get{return sampleWind!=null&&!failed;}}
 public static void Discover(){
  if(tried)return;tried=true;
  try {foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){
   var type=assembly.GetType("GroundBlastFx.Core.WindField",false);if(type==null)continue;
   var method=type.GetMethod("At",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(CelestialBody),typeof(Vector3)},null);
   if(method==null||method.ReturnType!=typeof(Vector3))continue;
   sampleWind=(Func<CelestialBody,Vector3,Vector3>)Delegate.CreateDelegate(typeof(Func<CelestialBody,Vector3,Vector3>),method);
   var config=assembly.GetType("GroundBlastFx.Config.GeSettings",false);var field=config==null?null:config.GetField("Renderer");settings=field==null?null:field.GetValue(null);
   brightness=settings==null?null:settings.GetType().GetField("EffectBrightness");Debug.Log("[KDA] GroundFX tire-smoke compatibility: shared wind and brightness; independent grounded tire particles");break;
  }}catch(Exception e){Disable(e);}
 }
 static void Disable(Exception e){failed=true;sampleWind=null;light=1;Debug.LogWarning("[KDA] GroundFX smoke integration unavailable; keeping KDA smoke: "+e.Message);}
 public static float Brightness {get{
  if(!failed&&brightness!=null&&Time.unscaledTime>=nextSettings){nextSettings=Time.unscaledTime+.5f;try{float value=(float)brightness.GetValue(settings);if(!float.IsNaN(value)&&!float.IsInfinity(value))light=Mathf.Clamp(value,.5f,1.5f);}catch(Exception e){Disable(e);}}
  return light;
 }}
 public static Vector3 Wind(CelestialBody body,Vector3 point){
  if(!Available)return Vector3.zero;
  try{Vector3 result=sampleWind(body,point);if(float.IsNaN(result.sqrMagnitude)||float.IsInfinity(result.sqrMagnitude))return Vector3.zero;return Vector3.ClampMagnitude(result,12);}
  catch(Exception e){Disable(e);return Vector3.zero;}
 }
}
