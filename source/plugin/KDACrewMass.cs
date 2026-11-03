using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// Only KDA's attached cosmetic crew parts are affected. Kerbals, their items,
// resources and EVA parts are retained; the stock global crew mass stays intact.
[KSPAddon(KSPAddon.Startup.MainMenu,true)]
public sealed class KDACrewMassPatches:MonoBehaviour {
 static readonly FieldInfo[] fields={AccessTools.Field(typeof(Part),"kerbalMass"),AccessTools.Field(typeof(Part),"kerbalInventoryMass"),AccessTools.Field(typeof(Part),"kerbalResourceMass"),AccessTools.Field(typeof(Part),"inventoryMass")};
 void Awake(){var harmony=new Harmony("kda.cosmetic.crew.mass");var callback=new HarmonyMethod(typeof(KDACrewMassPatches),"Normalize"){priority=Priority.Last};harmony.Patch(AccessTools.Method(typeof(Part),"FixedUpdate"),postfix:callback);harmony.Patch(AccessTools.Method(typeof(Part),"UpdateMass"),postfix:callback);DontDestroyOnLoad(gameObject);}
 public static void Normalize(Part __instance){
  var p=__instance;if(p==null||p.parent==null||p.partInfo==null)return;
  string name=p.partInfo.name;if(name!="kda-race-seat"&&name!="kda-doorway-marker")return;
  if(!HighLogic.LoadedSceneIsFlight&&!HighLogic.LoadedSceneIsEditor)return;
  // PhysicsSignificance=1 in the companion config merges the cosmetic part;
  // zero mass therefore never creates a zero-mass independent Rigidbody.
  p.mass=0;p.physicsMass=0;
  foreach(var field in fields)if(field!=null)field.SetValue(p,0f);
 }
}
