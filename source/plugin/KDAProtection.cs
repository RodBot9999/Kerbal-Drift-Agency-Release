using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

// Pure gate for the manual recovery button; ordinary driving never uses it.
public static class FlipRecoveryCore {
 public static bool Eligible(bool active,bool landed,bool packed,bool splashed,double upDot,double speed,double angularSpeed){
  return active&&landed&&!packed&&!splashed&&!double.IsNaN(upDot)&&!double.IsNaN(speed)&&!double.IsNaN(angularSpeed)
   &&upDot<=-.5&&upDot>=-1.001&&speed>=0&&speed<=.5&&angularSpeed>=0&&angularSpeed<=.2;
 }
}

// Local protection for this vehicle; never changes the global cheat settings.
public class KDAProtection : PartModule {
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="Invincible car")]
 public bool invincible=true;
 class Original {
  public float Crash,Force,Torque,G,Pressure;
  public double Temperature,SkinTemperature; public bool Enhancer;
  public CollisionEnhancer EnhancerObject;public Rigidbody Body;
  public CollisionDetectionMode Detection;
 }
 readonly Dictionary<Part,Original> originals=new Dictionary<Part,Original>();

 float flipSettledSince=-1,nextFlipAllowed;
 public int FlipCount {get;private set;}
 public bool CanFlip {get{return FlipEligible()&&flipSettledSince>=0&&Time.fixedTime-flipSettledSince>=.75f&&Time.fixedTime>=nextFlipAllowed;}}
 public override void OnStart(StartState state){base.OnStart(state);Events["FlipUpright"].active=false;}
 bool FlipEligible(){
  if(!HighLogic.LoadedSceneIsFlight||part==null||vessel==null||part.rb==null||part!=vessel.rootPart||part.FindModuleImplementing<KDAEngine>()==null)return false;
  return FlipRecoveryCore.Eligible(vessel==FlightGlobals.ActiveVessel,vessel.Landed,vessel.packed,vessel.Splashed,
   Vector3.Dot(-part.transform.forward,(Vector3)vessel.upAxis),vessel.srfSpeed,part.rb.angularVelocity.magnitude);
 }
 // KSP can skip OnFixedUpdate for an unactivated rover body. Both protection
 // and recovery UI must run through Unity's callback, regardless of staging.
 float protectionStep=float.NegativeInfinity,nextTerrainLog;
 public int TerrainRecoveries {get;private set;}
 public int TerrainKillsPrevented {get;private set;}
 void FixedUpdate(){if(HighLogic.LoadedSceneIsFlight&&part!=null&&vessel!=null){UpdateFlipAvailability();ApplyProtection();}}
 void UpdateFlipAvailability(){
  if(FlipEligible()){if(flipSettledSince<0)flipSettledSince=Time.fixedTime;}else flipSettledSince=-1;
  Events["FlipUpright"].active=CanFlip;
 }
 [KSPEvent(guiActive=true,guiActiveEditor=false,guiName="Flip upright",active=false)]
 public void FlipUpright(){TryFlipUpright();}
 public bool TryFlipUpright(){
  if(!CanFlip)return false;
  var colliders=new HashSet<Collider>();foreach(Part p in vessel.parts)foreach(var c in p.GetComponentsInChildren<Collider>(true))colliders.Add(c);
  Vector3 origin=part.transform.position,up=((Vector3)vessel.upAxis).normalized;
  RaycastHit ground=new RaycastHit();float closest=3;bool found=false;
  foreach(var hit in Physics.RaycastAll(origin+up*3,-up,6,~0,QueryTriggerInteraction.Ignore)){
   if(colliders.Contains(hit.collider)||Vector3.Dot(hit.normal,up)<.5f)continue;
   float depth=Vector3.Dot(origin-hit.point,up);if(depth<-.1f||depth>=closest)continue;
   closest=depth;ground=hit;found=true;
  }
  if(!found){ScreenMessages.PostScreenMessage("No ground found for flip recovery",3,ScreenMessageStyle.UPPER_CENTER);return false;}
  Vector3 normal=ground.normal.normalized,heading=Vector3.ProjectOnPlane(part.transform.up,normal).normalized;
  if(heading.sqrMagnitude<.5f)return false;
  Quaternion target=Quaternion.LookRotation(-normal,heading),delta=target*Quaternion.Inverse(part.transform.rotation);
  float lowest=float.PositiveInfinity;
  foreach(var c in colliders){
   if(c==null||!c.enabled||c.isTrigger||c is WheelCollider||!c.gameObject.activeInHierarchy)continue;
   Bounds b=c.bounds;foreach(var corner in Corners(b))lowest=Mathf.Min(lowest,Vector3.Dot(origin+delta*(corner-origin)-ground.point,normal));
  }
  foreach(Part p in vessel.parts){var b=p.FindModuleImplementing<ModuleWheelBase>();if(b!=null&&b.Wheel!=null)lowest=Mathf.Min(lowest,Vector3.Dot(origin+delta*(b.Wheel.WheelCenter-origin)-ground.point,normal)-b.Wheel.WheelRadius);}
  if(float.IsInfinity(lowest)||float.IsNaN(lowest))return false;
  float lift=Mathf.Max(0,.12f-lowest);
  if(lift>3){ScreenMessages.PostScreenMessage("Not enough clearance for flip recovery",3,ScreenMessageStyle.UPPER_CENTER);return false;}
  Vector3 offset=normal*lift;
  foreach(var c in colliders){
   if(c==null||!c.enabled||c.isTrigger||c is WheelCollider||!c.gameObject.activeInHierarchy)continue;
   Bounds b=c.bounds;Vector3 at=origin+delta*(b.center-origin)+offset;
   foreach(var obstacle in Physics.OverlapBox(at,b.extents,delta,~0,QueryTriggerInteraction.Ignore))if(!colliders.Contains(obstacle)){
    ScreenMessages.PostScreenMessage("Upright position is obstructed",3,ScreenMessageStyle.UPPER_CENTER);return false;
   }
  }
  // Use native vessel transforms so every attached part moves together.
  Vector3d position=vessel.GetWorldPos3D();Quaternion rotation=delta*vessel.transform.rotation;
  vessel.SetRotation(rotation,true);vessel.SetPosition(position+(Vector3d)offset,true);
  vessel.ChangeWorldVelocity(-vessel.srf_velocity);
  var bodies=new HashSet<Rigidbody>();foreach(Part p in vessel.parts){
   if(p.rb!=null&&bodies.Add(p.rb))p.rb.angularVelocity=Vector3.zero;
   if(p.servoRb!=null&&bodies.Add(p.servoRb))p.servoRb.angularVelocity=Vector3.zero;
   var b=p.FindModuleImplementing<ModuleWheelBase>();if(b!=null&&b.Wheel!=null&&b.Wheel.currentState!=null)b.Wheel.currentState.angularVelocity=0;
  }
  Physics.SyncTransforms();flipSettledSince=-1;nextFlipAllowed=Time.fixedTime+2;Events["FlipUpright"].active=false;FlipCount++;
  ScreenMessages.PostScreenMessage("Car flipped upright",2,ScreenMessageStyle.UPPER_CENTER);return true;
 }
 static IEnumerable<Vector3> Corners(Bounds b){for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)yield return b.center+Vector3.Scale(b.extents,new Vector3(x,y,z));}

 public override void OnFixedUpdate(){ApplyProtection();}
 void ApplyProtection(){
  if(!HighLogic.LoadedSceneIsFlight||vessel==null)return;
  if(protectionStep==Time.fixedTime)return;protectionStep=Time.fixedTime;
  RecoverTerrainOverlap();
  foreach(Part p in vessel.parts){
   if(!originals.ContainsKey(p))originals[p]=new Original{Crash=p.crashTolerance,Temperature=p.maxTemp,SkinTemperature=p.skinMaxTemp,Force=p.breakingForce,Torque=p.breakingTorque,G=p.gExplodeChance,Pressure=p.presExplodeChance};
   var o=originals[p];
   // Native rigidbodies/enhancers can be created after the first module tick.
   // Capture their real defaults when each component actually becomes available.
   if(p.collisionEnhancer!=null&&o.EnhancerObject!=p.collisionEnhancer){o.EnhancerObject=p.collisionEnhancer;o.Enhancer=p.collisionEnhancer.enabled;}
   if(p.rb!=null&&o.Body!=p.rb){o.Body=p.rb;o.Detection=p.rb.collisionDetectionMode;}
   p.crashTolerance=invincible?1e9f:o.Crash;p.maxTemp=invincible?1e9f:o.Temperature;p.skinMaxTemp=invincible?1e9f:o.SkinTemperature;
   p.breakingForce=invincible?1e9f:o.Force;p.breakingTorque=invincible?1e9f:o.Torque;
   p.gExplodeChance=invincible?0:o.G;p.presExplodeChance=invincible?0:o.Pressure;
   // This stock safeguard destroys parts on terrain penetration regardless of
   // crash tolerance. The rigid chassis still collides through its box collider.
   if(p.collisionEnhancer!=null)p.collisionEnhancer.enabled=invincible?false:o.Enhancer;
   if(p.rb!=null)p.rb.collisionDetectionMode=invincible?CollisionDetectionMode.ContinuousDynamic:o.Detection;
  }
 }
 void RecoverTerrainOverlap(){
  if(!invincible||vessel==null||vessel.packed||!vessel.loaded||vessel.terrainAltitude<0||double.IsNaN(vessel.altitude)||double.IsInfinity(vessel.altitude)||double.IsNaN(vessel.terrainAltitude)||double.IsInfinity(vessel.terrainAltitude))return;
  double lift=Math.Min(2,vessel.terrainAltitude+.08-vessel.altitude);if(lift<=0)return;
  vessel.SetPosition(vessel.GetWorldPos3D()+vessel.upAxis*lift,true);TerrainRecoveries++;
 }
 public static bool PreventTerrainKill(Vessel car){
  if(!HighLogic.LoadedSceneIsFlight||car==null||!car.loaded||car.packed||car.rootPart==null||car.terrainAltitude<0||double.IsNaN(car.altitude)||double.IsInfinity(car.altitude)||double.IsNaN(car.terrainAltitude)||double.IsInfinity(car.terrainAltitude))return false;
  var protection=car.rootPart.FindModuleImplementing<KDAProtection>();if(protection==null||!protection.invincible||car.rootPart.FindModuleImplementing<KDAEngine>()==null)return false;
  // Called only after the native under-terrain comparison has failed. Repair
  // the protected car here as well: this check can run between physics ticks.
  double depth=car.terrainAltitude-car.altitude;protection.RecoverTerrainOverlap();protection.TerrainKillsPrevented++;
  if(Time.realtimeSinceStartup>=protection.nextTerrainLog){protection.nextTerrainLog=Time.realtimeSinceStartup+1;Debug.Log("[KDA Protection] Prevented protected-car terrain kill; penetration="+depth.ToString("F3")+"m, speed="+car.srfSpeed.ToString("F1")+"m/s");}return true;
 }
}

// Preserve every other native kill condition and every unprotected vessel.
// Only the under-terrain destruction branch honors KDA's existing local toggle.
[KSPAddon(KSPAddon.Startup.MainMenu,true)]
public sealed class KDATerrainKillPatch:MonoBehaviour {
 void Awake(){try{new Harmony("kda.protected.car.terrain").Patch(AccessTools.Method(typeof(Vessel),"CheckKill"),transpiler:new HarmonyMethod(typeof(KDATerrainKillPatch),"GuardTerrainBranch"));Debug.Log("[KDA Protection] Scoped terrain-kill guard enabled");}catch(Exception e){Debug.LogError("[KDA Protection] Could not enable terrain-kill guard: "+e);}}
 static IEnumerable<CodeInstruction> GuardTerrainBranch(IEnumerable<CodeInstruction> instructions){
  var code=new List<CodeInstruction>(instructions);int marker=-1;for(int i=0;i<code.Count;i++)if(code[i].operand is string&&((string)code[i].operand).Contains("crashed through terrain")){if(marker>=0)throw new InvalidOperationException("Multiple native terrain-kill markers");marker=i;}
  int branch=-1;for(int i=marker-1;i>=0;i--)if(code[i].opcode==OpCodes.Bge_Un||code[i].opcode==OpCodes.Bge_Un_S){branch=i;break;}
  if(marker<0||branch<0||!(code[branch].operand is Label))throw new InvalidOperationException("Unexpected native terrain-kill layout");
  var first=new CodeInstruction(OpCodes.Ldarg_0);first.labels.AddRange(code[branch+1].labels);code[branch+1].labels.Clear();
  code.InsertRange(branch+1,new[]{first,new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(KDAProtection),"PreventTerrainKill")),new CodeInstruction(OpCodes.Brtrue,code[branch].operand)});return code;
 }
}

