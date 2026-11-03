// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

[KSPAddon(KSPAddon.Startup.Instantly,true)]
public sealed class KDAECUBootstrap : MonoBehaviour {
 void Awake(){try{new Harmony("KerbalDriftAgency.OptionalRoverECU").PatchAll(typeof(KDAECUBootstrap).Assembly);EcuHooks.Ready=true;}catch(Exception ex){EcuHooks.Ready=false;Debug.LogError("[KDA ECU] Hook installation failed; native rovers remain untouched: "+ex);}}
}
internal static class EcuHooks {
 internal static bool Ready;
 internal static readonly Dictionary<KSPWheelController,EcuWheel> Wheels=new Dictionary<KSPWheelController,EcuWheel>();
 static readonly Dictionary<PartModule,EcuWheel> Modules=new Dictionary<PartModule,EcuWheel>();
 internal static void Register(EcuWheel w){Wheels[w.Wheel]=w;foreach(PartModule m in w.NativeModules)if(!ReferenceEquals(m,null))Modules[m]=w;}
 internal static void Unregister(EcuWheel w){EcuWheel existing;if(Wheels.TryGetValue(w.Wheel,out existing)&&existing==w)Wheels.Remove(w.Wheel);foreach(PartModule m in w.NativeModules)if(!ReferenceEquals(m,null)&&Modules.TryGetValue(m,out existing)&&existing==w)Modules.Remove(m);}
 [HarmonyPatch(typeof(KSPWheelController),"OnUpdate")]
 internal static class SteeringPatch {
  [HarmonyPrefix] static void Prefix(KSPWheelController __instance){
   EcuWheel w;if(!Wheels.TryGetValue(__instance,out w))return;
   try{w.ApplySteering();}catch(Exception ex){w.Owner.Release();w.Owner.ecuEnabled=false;w.Owner.ecuStatus="ECU steering error - native rover restored";Debug.LogError("[KDA ECU] "+ex);}
  }
 }
 [HarmonyPatch(typeof(KSPWheelController),"DoUpdateBlocks")]
 internal static class PhysicsPatch {
  [HarmonyPrefix] static void Prefix(KSPWheelController __instance,out EcuWheel.Frame __state){__state=new EcuWheel.Frame();EcuWheel w;if(Wheels.TryGetValue(__instance,out w))try{__state=w.CaptureFrame();w.Before(__state);}catch(Exception ex){w.After(__state);w.Owner.Release();w.Owner.ecuEnabled=false;w.Owner.ecuStatus="ECU error - native rover restored";Debug.LogError("[KDA ECU] "+ex);}}
  [HarmonyPostfix] static void Postfix(KSPWheelController __instance,EcuWheel.Frame __state){EcuWheel w;if(Wheels.TryGetValue(__instance,out w))w.After(__state);}
  [HarmonyFinalizer] static Exception Finalizer(KSPWheelController __instance,EcuWheel.Frame __state,Exception __exception){if(__exception!=null){EcuWheel w;if(Wheels.TryGetValue(__instance,out w)){w.After(__state);w.Owner.Release();}}return __exception;}
 }
 [HarmonyPatch(typeof(PartModule),"Save")]
 internal static class SavePatch {
  [HarmonyPostfix] static void Postfix(PartModule __instance,ConfigNode __0){EcuWheel w;if(Modules.TryGetValue(__instance,out w))w.SaveNative(__instance,__0);}
 }
}
