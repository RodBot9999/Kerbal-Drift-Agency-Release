// SPDX-License-Identifier: MIT
using System;
using UnityEngine;
using KerbalDriftAgency.ECU;

public sealed partial class KDAECU {
 [KSPField(isPersistant=true,guiActive=true,guiActiveEditor=true,guiName="SC profile")]
 [UI_ChooseOption(options=new[]{"Unstable","Normal"})] public int addonProfile;
 [KSPField(guiActive=true,guiName="Pitch support")] public string addonPitchStatus="Standby";
 [KSPField(guiActive=true,guiName="Front brake allowance",guiFormat="P0")] public float addonBrakeScale=1;
 readonly AddonSteering addonSteering=new AddonSteering();readonly AddonPitchGuard addonPitch=new AddonPitchGuard();
 int lastAddonProfile=-1;float addonPhysicsStep=-1;double baselineRearShare=.5;bool baselineReady;
 Vector3 addonGroundNormal=Vector3.zero;
 internal float SteeringDegrees;
 internal float SteeringOrientation(EcuWheel wheel){
  if(wheel.Wheel.wheelCollider==null||wheel.Wheel.wheelCollider.cachedTransform==null)return 0;
  float dot=Vector3.Dot(wheel.Wheel.wheelCollider.cachedTransform.up,part.transform.up);
  return Mathf.Abs(dot)<.5f?0:Mathf.Sign(dot);
 }
 void AddonPresentation(){
  addonProfile=AddonProfile.Normalize(addonProfile);
  Fields["addonPitchStatus"].guiActive=Fields["addonBrakeScale"].guiActive=ecuEnabled;
  if(!Active)return;
  if(lastAddonProfile!=addonProfile){ResetAddonState();lastAddonProfile=addonProfile;}
 }
 void ResetAddonState(){addonSteering.Reset();addonPitch.Reset();SteeringDegrees=0;addonBrakeScale=1;addonPitchStatus="Standby";addonPhysicsStep=-1;lastAddonProfile=-1;baselineReady=false;baselineRearShare=.5;addonGroundNormal=Vector3.zero;}
 void DriveAddon(FlightCtrlState state){
  AddonPresentation();Vector3 velocity=(Vector3)vessel.srf_velocity;
  float speed=Vector3.Dot(velocity,part.transform.forward),sideSpeed=Vector3.Dot(velocity,part.transform.right),angle=180,left=0,right=0;int contacts=0;Vector3 normal=Vector3.zero;
  foreach(var w in Wheels){if(w.FrontSteer)angle=Mathf.Min(angle,w.ControlAngle);if(w.Wheel.currentState==null||!w.Wheel.IsGrounded)continue;contacts++;normal+=w.Wheel.currentState.hit.normal;float load=Mathf.Max(0,w.Wheel.currentState.downforce);if(Local(w.Part).x<SteeringCentreX)left+=load;else right+=load;}
  float gravity=(float)FlightGlobals.getGeeForceAtPosition(part.transform.position).magnitude;
  effectiveBudget=(float)EcuCore.Budget(corneringPercent,track,cgHeight);
  float slip=Mathf.Atan2(sideSpeed,speed)*Mathf.Rad2Deg,yaw=part.rb==null?0:Vector3.Dot(part.rb.angularVelocity,part.transform.up),roll=normal.sqrMagnitude>.01f?Vector3.Angle(part.transform.up,normal.normalized):0;
  state.wheelSteer=(float)addonSteering.Step(Time.fixedDeltaTime,AddonProfile.Normalize(addonProfile),adaptiveSteering,yawStability,assistance,state.wheelSteer+state.wheelSteerTrim,speed,angle,wheelbase,gravity,slip,yaw,roll,Math.Abs(left-right)/Math.Max(.01,left+right),effectiveBudget,contacts>=2)-state.wheelSteerTrim;
  SteeringDegrees=(state.wheelSteer+state.wheelSteerTrim)*angle;
  YawError=yawStability?(float)addonSteering.YawError:0;
 }
 internal void PrepareAddonPhysics(){
  if(!Active||addonPhysicsStep==Time.fixedTime)return;addonPhysicsStep=Time.fixedTime;
  int rearCount=0,rearContacts=0,frontContacts=0;double rear=0,total=0,brake=0;bool rearEmpty=false;Vector3 normal=Vector3.zero;
  foreach(var w in Wheels){var state=w.Wheel.currentState;if(state==null)continue;bool front=IsFront(w),contact=w.Wheel.IsGrounded;double load=contact?Math.Max(0,state.downforce):0;
   total+=load;if(!front){rearCount++;rear+=load;if(contact)rearContacts++;if(!contact||load<=.0001)rearEmpty=true;}else if(contact&&load>.0001)frontContacts++;
   if(contact)normal+=state.hit.normal;brake=Math.Max(brake,w.Wheel.keyboardBrakes?(Input.GetKey(KeyCode.Space)?1:0):w.Wheel.brakeInput);
  }
  if(normal.sqrMagnitude>.01f){Vector3 measured=normal.normalized;if(addonGroundNormal.sqrMagnitude<.01f)addonGroundNormal=measured;else addonGroundNormal=Vector3.Slerp(addonGroundNormal,measured,Mathf.Clamp01(Time.fixedDeltaTime*4));}
  double fraction=total>.001?rear/total:0;
  if(brake<.02&&rearCount>0&&rearContacts==rearCount&&frontContacts>0&&total>.001){baselineRearShare=baselineReady?baselineRearShare+(fraction-baselineRearShare)*Math.Min(1,Time.fixedDeltaTime*2):fraction;baselineReady=true;}
  float speed=Vector3.Dot((Vector3)vessel.srf_velocity,part.transform.forward);
  double nose=addonGroundNormal.sqrMagnitude>.01f?-Math.Asin(Math.Max(-1,Math.Min(1,Vector3.Dot(part.transform.forward,addonGroundNormal))))*180/Math.PI:0;
  double rate=part.rb==null?0:Vector3.Dot(part.rb.angularVelocity,part.transform.right);
  addonBrakeScale=rearCount==0?1:(float)addonPitch.Step(Time.fixedDeltaTime,AddonProfile.Normalize(addonProfile),brake,speed,nose,rate,fraction,baselineRearShare,rearEmpty,frontContacts>0);
  addonPitchStatus=addonBrakeScale<.99?(addonProfile==0?"Early nose-dive relief":"Reactive: rear wheel unloaded"):addonProfile==0?"Watching pitch / rear load":"Waiting for full rear-wheel unloading";
 }
 internal float AddonBrakeAllowance(EcuWheel wheel){return IsFront(wheel)?addonBrakeScale:1;}
}
