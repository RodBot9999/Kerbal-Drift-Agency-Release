using UnityEngine;

// Cosmetic IVA controller. The road-wheel modules remain the physics owners.
public class KDASteeringWheel : InternalModule {
 [KSPField] public string steeringTransform="SteeringTurn";
 [KSPField] public float fullLockDegrees=540;
 [KSPField] public float turnRate=1080;
 Transform pivot;Quaternion neutral;float angle;
 void Update(){
  if(!HighLogic.LoadedSceneIsFlight||internalProp==null||vessel==null||vessel.packed)return;
  if(pivot==null){pivot=internalProp.FindModelTransform(steeringTransform);if(pivot==null)return;neutral=pivot.localRotation;angle=0;}
  float input=Mathf.Clamp(vessel.ctrlState.wheelSteer+vessel.ctrlState.wheelSteerTrim,-1,1);
  angle=Mathf.MoveTowards(angle,input*fullLockDegrees,Mathf.Max(1,turnRate)*Time.deltaTime);
  pivot.localRotation=neutral*Quaternion.AngleAxis(angle,Vector3.up);
 }
}
