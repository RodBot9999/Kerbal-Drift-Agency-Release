using System;
using UnityEngine;
using KerbalDriftAgency;

// Cosmetic outlet. Local +Y is the direction of the visible editor arrow.
public class KDAExhaust : PartModule {
 [KSPField] public bool integratedOutlet=false;
 [KSPField] public string outletPosition="0,0,0",outletDirection="0,1,0";
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Exhaust flames")] public bool flamesEnabled=true;
 KDAEngine engine;ParticleSystem flames;Material material;Texture2D texture;GameObject arrow;
 int lastSerial;float nitrousParticles,fireParticles;Gradient normalFlame,nitrousFlame,firePlume;
 public int FlameEvents {get;private set;}
 public int ParticleCount {get{return flames==null?0:flames.particleCount;}}
 public override void OnStart(StartState s){
  base.OnStart(s);
  if(HighLogic.LoadedSceneIsEditor){
   if(integratedOutlet){Fields["flamesEnabled"].guiActiveEditor=false;return;}
   arrow=GameObject.CreatePrimitive(PrimitiveType.Cylinder);arrow.name="Exhaust direction +Y";arrow.transform.SetParent(part.transform,false);arrow.transform.localPosition=new Vector3(0,.10f,0);arrow.transform.localScale=new Vector3(.02f,.1f,.02f);Destroy(arrow.GetComponent<Collider>());
   arrow.GetComponent<Renderer>().material.color=new Color(.2f,1,.25f);return;
  }
  if(!HighLogic.LoadedSceneIsFlight)return;
  if(!integratedOutlet){foreach(var r in part.GetComponentsInChildren<Renderer>())r.enabled=false;foreach(var c in part.GetComponentsInChildren<Collider>())c.enabled=false;}
  var shader=Shader.Find("Particles/Additive")??Shader.Find("Legacy Shaders/Particles/Additive");
  if(shader==null){Debug.LogError("[KDA] Flame shader unavailable");return;}
  texture=new Texture2D(32,32,TextureFormat.RGBA32,false);texture.wrapMode=TextureWrapMode.Clamp;
  var pixels=new Color[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++){float u=(x-15.5f)/15.5f,v=(y-15.5f)/15.5f;float a=Mathf.Pow(Mathf.Clamp01(1-u*u-v*v),2);pixels[y*32+x]=new Color(a,a,a,a);}texture.SetPixels(pixels);texture.Apply();
  material=new Material(shader);material.mainTexture=texture;
  var obj=new GameObject("KDA exhaust flame");Transform outletFrame=part.transform;
  if(integratedOutlet){var hardware=part.FindModelTransform("RigidHardware");outletFrame=hardware!=null?hardware.parent:part.FindModelTransform("model")??part.transform;}
  obj.transform.SetParent(outletFrame,false);
  Vector3 direction=Vector3.up;
  if(integratedOutlet){obj.transform.localPosition=ParseVector(outletPosition);direction=ParseVector(outletDirection).normalized;}
  obj.transform.localRotation=Quaternion.FromToRotation(Vector3.forward,direction);
  flames=obj.AddComponent<ParticleSystem>();flames.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
  var main=flames.main;main.loop=false;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.maxParticles=50;
  main.startLifetime=new ParticleSystem.MinMaxCurve(.06f,.14f);main.startSpeed=new ParticleSystem.MinMaxCurve(2.2f,4.2f);main.startSize=new ParticleSystem.MinMaxCurve(.05f,.11f);
  var emit=flames.emission;emit.enabled=false;var shape=flames.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=7;shape.radius=.018f;
  var color=flames.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.25f,.45f,1),0),new GradientColorKey(new Color(1,.65f,.12f),.3f),new GradientColorKey(new Color(1,.12f,.01f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=new ParticleSystem.MinMaxGradient(gradient);normalFlame=gradient;
  nitrousFlame=new Gradient();nitrousFlame.SetKeys(new[]{new GradientColorKey(new Color(.6f,.9f,1),0),new GradientColorKey(new Color(.08f,.35f,1),.5f),new GradientColorKey(new Color(.02f,.08f,.8f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});
  firePlume=new Gradient();firePlume.SetKeys(new[]{new GradientColorKey(new Color(1,.95f,.6f),0),new GradientColorKey(new Color(1,.50f,.055f),.3f),new GradientColorKey(new Color(.8f,.055f,.008f),1)},new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(.65f,.55f),new GradientAlphaKey(0,1)});
  obj.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;flames.Play();
 }
 static Vector3 ParseVector(string text){string[] values=text.Split(',');return new Vector3(float.Parse(values[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(values[1],System.Globalization.CultureInfo.InvariantCulture),float.Parse(values[2],System.Globalization.CultureInfo.InvariantCulture));}
 void Update(){
  if(flames==null||vessel==null||part.packed||FlightDriver.Pause||Time.timeScale==0||TimeWarp.CurrentRate>1)return;
  if(engine==null||engine.vessel!=vessel){engine=null;foreach(Part p in vessel.parts){engine=p.FindModuleImplementing<KDAEngine>();if(engine!=null)break;}}
  if(engine==null)return;
  var events=engine.Afterfire;if(events==null)return;bool burst=events.Serial!=lastSerial;lastSerial=events.Serial;
  bool nitrous=engine.NitrousActive&&vessel==FlightGlobals.ActiveVessel;
  var color=flames.colorOverLifetime;color.color=new ParticleSystem.MinMaxGradient(nitrous?nitrousFlame:engine.afterfireTune==2?firePlume:normalFlame);
  if(nitrous&&flamesEnabled){
   var main=flames.main;float strength=Mathf.Lerp(.8f,1.8f,Mathf.Clamp01(engine.nitrousShotHp/300));
   main.startSize=new ParticleSystem.MinMaxCurve(.06f*strength,.11f*strength);main.startSpeed=new ParticleSystem.MinMaxCurve(3*strength,5*strength);main.startLifetime=new ParticleSystem.MinMaxCurve(.10f,.18f);
   nitrousParticles+=Mathf.Min(.1f,Time.deltaTime)*90;int count=(int)nitrousParticles;nitrousParticles-=count;if(count>0)flames.Emit(count);
  }else nitrousParticles=0;
  if(!nitrous&&engine.afterfireTune==2&&events.FlamePlume>0&&flamesEnabled){
   float size=Mathf.Clamp(engine.flameSize,.25f,3),duration=Mathf.Clamp(engine.flameDuration,.5f,3);var main=flames.main;
   float strength=events.FlamePlume*(.92f+.08f*Mathf.Sin(Time.time*37+part.flightID));
   main.startSize=new ParticleSystem.MinMaxCurve(.055f*size,.115f*size);main.startSpeed=new ParticleSystem.MinMaxCurve(2.8f*size,5.2f*size);
   main.startLifetime=new ParticleSystem.MinMaxCurve(.10f*duration,Mathf.Min(.5f,.22f*duration));
   fireParticles+=Mathf.Min(.1f,Time.deltaTime)*90*strength;int count=(int)fireParticles;fireParticles-=count;if(count>0){flames.Emit(count);FlameEvents++;}
  }else fireParticles=0;
  if(burst&&!nitrous&&engine.afterfireTune!=2&&flamesEnabled&&(engine.afterfireTune!=1||events.Bang)){
   float size=Mathf.Clamp(engine.flameSize,.25f,3),duration=Mathf.Clamp(engine.flameDuration,.5f,3);var main=flames.main;
   main.startSize=new ParticleSystem.MinMaxCurve(.05f*size,.11f*size);main.startSpeed=new ParticleSystem.MinMaxCurve(2.2f*size,4.2f*size);main.startLifetime=new ParticleSystem.MinMaxCurve(.06f*duration,.14f*duration);
   flames.Emit(events.Bang?16:12);FlameEvents++;
  }
 }
 void OnDestroy(){if(flames!=null)Destroy(flames.gameObject);if(material!=null)Destroy(material);if(texture!=null)Destroy(texture);if(arrow!=null){var r=arrow.GetComponent<Renderer>();if(r!=null)Destroy(r.material);Destroy(arrow);}}
 public override string GetInfo(){return "Invisible in flight. Place at the tailpipe opening; green arrow points along the flame direction (+Y). Shared engine pops and bangs with synchronized sound. Tune frequency, volume, flame size and duration in Garage / Tune / Sound & FX. Cosmetic only; no thrust or fuel use.";}
}
