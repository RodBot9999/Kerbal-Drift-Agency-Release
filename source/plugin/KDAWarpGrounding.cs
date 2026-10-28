using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

// KSP's unpack clearance probe calls Collider.ClosestPoint on a Unity
// WheelCollider. Unity rejects that collider type and returns the probe itself,
// 1000 m below the vessel. Never change collider layers, suspension or forces.
[KSPAddon(KSPAddon.Startup.MainMenu,true)]
public sealed class KDAWarpGrounding:MonoBehaviour {
 void Awake(){
  try {
   new Harmony("kda.warp.wheel.clearance").Patch(AccessTools.Method(typeof(Vessel),"getLowestPoint"),transpiler:new HarmonyMethod(typeof(KDAWarpGrounding),"ReplaceProbe"));
   Debug.Log("[KDA] Time-warp wheel clearance fix enabled");
  }catch(Exception e){Debug.LogError("[KDA] Could not enable time-warp wheel clearance fix: "+e);}
 }
 static IEnumerable<CodeInstruction> ReplaceProbe(IEnumerable<CodeInstruction> instructions){
  var original=AccessTools.Method(typeof(Collider),"ClosestPoint",new[]{typeof(Vector3)});
  var replacement=AccessTools.Method(typeof(KDAWarpGrounding),"ClosestGroundPoint");
  int replaced=0;
  foreach(var instruction in instructions){
   if(instruction.Calls(original)){instruction.opcode=OpCodes.Call;instruction.operand=replacement;replaced++;}
   yield return instruction;
  }
  if(replaced!=1)throw new InvalidOperationException("Unexpected KSP clearance probe layout: "+replaced);
 }
 public static Vector3 ClosestGroundPoint(Collider collider,Vector3 probe){
  var wheel=collider as WheelCollider;
  var owner=wheel==null?null:wheel.GetComponentInParent<Part>();
  if(owner==null||owner.FindModuleImplementing<KDASuspension>()==null)return collider.ClosestPoint(probe);
  // getLowestPoint temporarily aligns the ground normal with world +Z.
  // WheelCollider.bounds is empty in KSP, so use its native center, travel
  // and radius instead. A drooped tire is a conservative clearance envelope.
  Vector3 end=wheel.transform.TransformPoint(wheel.center+Vector3.down*wheel.suspensionDistance);
  Vector3 point=end-Vector3.forward*wheel.radius;
  // Stock applies another radius and 0.15 m margin to this exact spelling.
  // Our generated wheels use lower-case wheelCollider, but preserve support
  // for existing models with the stock name without counting radius twice.
  if(collider.gameObject.name.Contains("WheelCollider")){
   var vp=collider.GetComponent<VehiclePhysics.VPWheelCollider>();
   point.z+=(vp==null?wheel.radius:vp.radius)+.15f;
  }
  return point;
 }
}
