using UnityEngine;

// Calibrates the chassis only. Wheels, fuel and ordinary payload stay native;
// source target is the assembled, full-fuel reference rather than body alone.
public sealed class KDAImportedMass:PartModule {
 [KSPField] public bool sourceValid;
 [KSPField] public float referenceMassKg,referenceFrontFraction;
 [KSPField] public Vector3 referenceCenter,bodyCenter;
 [KSPField(guiActive=true,guiActiveEditor=true,guiName="Mass balance")] public string status="Generic CG fallback";
 [KSPField(guiActive=true,guiActiveEditor=true,guiName="Source front weight",guiFormat="P1")] public float sourceFrontWeight;
 public override void OnStart(StartState s){base.OnStart(s);Apply();}
 void Apply(){
  if(!sourceValid||referenceMassKg<150||referenceMassKg>10000||referenceFrontFraction<.1f||referenceFrontFraction>.9f||float.IsNaN(bodyCenter.sqrMagnitude)||bodyCenter.sqrMagnitude>100||Mathf.Abs(part.rescaleFactor-1)>.001f){status="Generic CG fallback / no valid source calibration";return;}
  part.CoMOffset=bodyCenter;sourceFrontWeight=referenceFrontFraction;status="Source node mass distribution";
 }
 public override void OnFixedUpdate(){if(HighLogic.LoadedSceneIsFlight&&!part.packed)Apply();}
 public override string GetInfo(){return "Source-weighted chassis CG and front/rear balance. Reference is the complete car with full fuel. KDA seats and seated crew are massless; ordinary payload and fuel still count.";}
}
