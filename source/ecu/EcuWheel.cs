// SPDX-License-Identifier: MIT
using ModuleWheels;
using UnityEngine;
using KerbalDriftAgency.ECU;

internal sealed class EcuWheel {
 internal readonly KDAECU Owner;internal readonly ModuleWheelBase Base;internal readonly Part Part;internal readonly KSPWheelController Wheel;
 internal readonly PartModule[] NativeModules;internal readonly ModuleWheelSteering Steering;internal readonly ModuleWheelMotor Motor;
 internal bool FrontSteer;internal float OriginalAngle {get{return Steering==null?0:Mathf.Max(1,angle);}}
 internal float ControlAngle {get{return OriginalAngle;}}
 bool captured,nativeSteering,autoSteering,nativeKeyboard;float angle,range,response,nativeMaxAngle,nativeServo;ConfigNode savedSteering;
 readonly AntiLockCore abs=new AntiLockCore();readonly AidBrakeCore brakes=new AidBrakeCore();
 internal EcuWheel(KDAECU owner,ModuleWheelBase b){Owner=owner;Base=b;Part=b.part;Wheel=b.Wheel;Steering=Part.FindModuleImplementing<ModuleWheelSteering>();Motor=Part.FindModuleImplementing<ModuleWheelMotor>();NativeModules=new PartModule[]{Steering};}
 internal void Activate(bool steer){
  if(!captured){
   nativeKeyboard=Wheel.keyboardSteering;nativeServo=Wheel.steeringResponse;
   if(Steering!=null){savedSteering=new ConfigNode();Steering.Save(savedSteering);nativeSteering=Steering.steeringEnabled;autoSteering=Steering.autoSteeringAdjust;angle=Steering.angleTweakable;range=Steering.steeringRange;nativeMaxAngle=Wheel.maxSteerAngle;response=Steering.responseTweakable;}
   captured=true;EcuHooks.Register(this);
  }
  FrontSteer=steer;if(!steer){Wheel.steerInput=0;Wheel.steerState=0;}
  if(Steering!=null){Steering.steeringEnabled=steer;Steering.autoSteeringAdjust=steer&&!Owner.adaptiveSteering?autoSteering:false;Steering.responseTweakable=steer&&(Owner.adaptiveSteering||Owner.yawStability)?10:response;}
 }
 // One shared physical axle command, applied immediately before VPP reads it.
 // This bypasses stock command-pod/CoM turn centres and raw-key overrides.
 internal void ApplySteering(){
  if(!captured||!Owner.Active||!Owner.ecuEnabled||Owner.vessel==null||Part.vessel!=Owner.vessel||Owner.vessel.packed||Owner.vessel!=FlightGlobals.ActiveVessel)return;
  Wheel.keyboardSteering=false;
  float orientation=Owner.SteeringOrientation(this),degrees=FrontSteer?Owner.SteeringDegrees:0;
  bool broken=Base.InopSystems.HasType(WheelSubsystem.SystemTypes.WheelCollider|WheelSubsystem.SystemTypes.Tire);
  float target=broken||Mathf.Abs(orientation)<.5f?0:Mathf.Clamp(-degrees*orientation/Mathf.Max(1,Wheel.maxSteerAngle),-1,1);
  Wheel.steerInput=target;Wheel.steeringResponse=40;
  if(!FrontSteer||broken||Mathf.Abs(orientation)<.5f)Wheel.steerState=0;
 }
 internal struct Frame {internal bool Applied;internal float Brake,BrakeResponse;internal bool KeyboardBrake;}
 internal Frame CaptureFrame(){
  if(!captured||!Owner.Active||!Owner.ecuEnabled||Owner.vessel==null||Part.vessel!=Owner.vessel||Owner.vessel.packed||Owner.vessel!=FlightGlobals.ActiveVessel)return new Frame();
  return new Frame{Applied=true,Brake=Wheel.brakeInput,BrakeResponse=Wheel.brakeResponse,KeyboardBrake=Wheel.keyboardBrakes};
 }
 internal void Before(Frame frame){
  if(!frame.Applied)return;Owner.PrepareAddonPhysics();var state=Wheel.currentState;if(state==null)return;
  bool grounded=Wheel.IsGrounded;float road=state.localWheelVelocity.y,tread=Mathf.Abs(state.angularVelocity*Wheel.WheelRadius);
  float service=Wheel.keyboardBrakes?(Input.GetKey(KeyCode.Space)?1:0):Mathf.Clamp01(Wheel.brakeInput);
  bool working=Part.FindModuleImplementing<ModuleWheelBrakes>()!=null&&!Base.InopSystems.HasType(WheelSubsystem.SystemTypes.Brakes|WheelSubsystem.SystemTypes.WheelCollider|WheelSubsystem.SystemTypes.Tire);
  float traction=working&&Owner.brakeTraction&&Motor!=null&&Motor.motorEnabled?(float)AddonProfile.TractionBrake(Owner.addonProfile,road,tread,grounded):0;
  float yaw=working&&Owner.yawStability&&grounded?(float)AddonProfile.YawBrake(Owner.addonProfile,Owner.YawError,Owner.Local(Part).x-Owner.SteeringCentreX,FrontSteer):0;
  float aid=(float)brakes.Step(Time.fixedDeltaTime,Owner.brakeTraction,Owner.yawStability,traction,yaw);
  float requested=Mathf.Max(service,aid)*Owner.AddonBrakeAllowance(this),scale=(float)abs.Step(Time.fixedDeltaTime,Owner.absEnabled,grounded,road,tread,requested);
  Wheel.keyboardBrakes=false;Wheel.brakeInput=requested*scale;
  if(Owner.absEnabled||aid>0||Owner.AddonBrakeAllowance(this)<.999f){Wheel.brakeResponse=100;Wheel.brakeState=Mathf.Min(Wheel.brakeState,Wheel.brakeInput);}
 }
 internal void After(Frame frame){if(!frame.Applied)return;Wheel.brakeInput=frame.Brake;Wheel.brakeResponse=frame.BrakeResponse;Wheel.keyboardBrakes=frame.KeyboardBrake;}
 internal void SaveNative(PartModule module,ConfigNode node){
  if(!captured||module!=Steering||savedSteering==null)return;
  foreach(string key in new[]{"steeringEnabled","autoSteeringAdjust","responseTweakable","angleTweakable","steeringRange"})if(savedSteering.HasValue(key))node.SetValue(key,savedSteering.GetValue(key),true);
 }
 internal void Restore(){
  EcuHooks.Unregister(this);if(!captured)return;captured=false;if(Part==null)return;
  if(Wheel!=null){Wheel.keyboardSteering=nativeKeyboard;Wheel.steeringResponse=nativeServo;}
  if(Steering!=null){Steering.steeringEnabled=nativeSteering;Steering.autoSteeringAdjust=autoSteering;Steering.angleTweakable=angle;Steering.steeringRange=range;Steering.responseTweakable=response;Steering.steeringInput=0;if(Wheel!=null){Wheel.steerInput=0;Wheel.steerState=0;Wheel.maxSteerAngle=nativeMaxAngle;}}
  if(Wheel!=null)Wheel.brakeState=Wheel.brakeInput;abs.Step(0,false,false,0,0,0);brakes.Reset();
 }
}
