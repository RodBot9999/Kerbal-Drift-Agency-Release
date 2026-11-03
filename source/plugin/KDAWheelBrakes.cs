using System;
using ModuleWheels;
using UnityEngine;

// Keep stock brake actions/setup and combine compression/rolling resistance in
// the same wheel brake input that the native physics block consumes.
public class KDAWheelBrakes : ModuleWheelBrakes {
 KDAWheelMotor motor;KDAEngine engine;
 float aidPressure;
 object capacityWheel;float baseBrakeTorque,lastBrakeCapacity;
 public float AidPressure {get{return aidPressure;}}
 public float AppliedDrag {get;private set;}
 readonly AntiLockCore abs=new AntiLockCore();
 readonly AidBrakeCore aids=new AidBrakeCore();
 public float AbsScale {get{return (float)abs.Scale;}}
 public override void OnStart(StartState state){
  base.OnStart(state);motor=part.FindModuleImplementing<KDAWheelMotor>();
  if(HighLogic.LoadedSceneIsFlight)foreach(PartModule module in part.Modules){
   foreach(BaseField field in module.Fields)if(field.guiActiveEditor)field.guiActive=false;
   foreach(BaseEvent evt in module.Events)if(evt.guiActiveEditor)evt.guiActive=false;
  }
 }
 // Unity calls the derived message rather than the stock private FixedUpdate.
 void FixedUpdate(){
  if(!HighLogic.LoadedSceneIsFlight||!baseSetup||part.packed||wheelBase==null||wheelBase.Wheel==null)return;
  var controller=wheelBase.Wheel;
  if(engine==null||engine.vessel!=vessel){engine=null;foreach(Part p in vessel.parts){engine=p.FindModuleImplementing<KDAEngine>();if(engine!=null)break;}}
  // Grow service-brake capacity above 100%, but normalize every independent
  // drag/aid/handbrake request so their absolute torque remains unchanged.
  if(!ReferenceEquals(capacityWheel,controller)||Mathf.Abs(controller.maxBrakeTorque-lastBrakeCapacity)>.001f){capacityWheel=controller;baseBrakeTorque=controller.maxBrakeTorque;}
  float power=engine==null?1:Mathf.Clamp(engine.brakePowerPercent,0,200)*.01f,capacity=Mathf.Max(1,power);
  controller.maxBrakeTorque=lastBrakeCapacity=baseBrakeTorque*capacity;
  double omega=Math.Abs(controller.currentState.angularVelocity),rpm=omega*60/(2*Math.PI);
  float torque=engine!=null?engine.DragPerWheel(rpm,motor!=null&&motor.motorEnabled&&motor.CanDrive):0;
  if(engine!=null&&engine.RearDrivelineCoupling<1)torque*=engine.DriveClutchForWheel(motor);
  if(!controller.IsGrounded&&motor!=null&&!motor.motorEnabled)torque*=.25f;
  AppliedDrag=torque;
  if(engine!=null&&engine.BurnoutActive){controller.brakeInput=engine.DrivingEffects.IsFront(part)?1/capacity:0;controller.brakeResponse=100;AppliedDrag=0;aidPressure=0;aids.Reset();return;}
  bool brakesWorking=!wheelBase.InopSystems.HasType(WheelSubsystem.SystemTypes.Brakes);
  float service=brakesWorking?Mathf.Clamp01(ApplyTorqueAdjustments(brakeInput)*brakeTweakable*.01f):0;
  var controls=engine==null?null:engine.part.FindModuleImplementing<KDACarControls>();
  var sc=engine==null?null:engine.part.FindModuleImplementing<KDAController>();
  // Drift's normal brake group (B) becomes a front-only pedal. Rear
  // driveline drag and the separately bound rear handbrake remain independent.
  bool front=controls!=null?controls.Front(part):engine!=null&&engine.DrivingEffects.IsFront(part);
  service=(float)DriverAidsCore.ServiceBrake(service,sc==null?2:sc.driveMode,front);
  service*=power/capacity;
  float requested=Mathf.Clamp01(service+torque/Mathf.Max(.01f,controller.maxBrakeTorque));
  bool tractionEnabled=brakesWorking&&controls!=null&&controls.TcsActive&&motor!=null&&motor.motorEnabled&&motor.CanDrive&&engine.ProducingPower&&engine.AllowsDriveInput;
  float traction=tractionEnabled?(float)DriverAidsCore.TractionBrake(controller.currentState.localWheelVelocity.y,omega*controller.WheelRadius,controller.IsGrounded,sc==null?2:sc.driveMode):0;
  if(tractionEnabled&&sc!=null&&RoadSportCore.Applies(sc.driveMode))traction=(float)RoadSportCore.TractionBrake(controller.currentState.localWheelVelocity.y,omega*controller.WheelRadius,controller.IsGrounded,sc.driveMode);
  if(engine!=null&&engine.BurnoutLaunching)traction*=engine.DrivingEffects.LaunchTcsBlend;
  float yaw=brakesWorking&&controller.IsGrounded&&sc!=null?sc.StabilityBrake(part):0;
  // Torque/traction brakes belong to TCS; selective yaw brakes belong to SC.
  // Switching TCS off must not remove SC's brake support, and SC off must
  // immediately remove any old yaw request rather than leave brake pressure.
  bool stabilityBrakes=brakesWorking&&sc!=null&&sc.stabilityEnabled&&sc.assistance>0&&sc.driveMode!=3;
  aidPressure=(float)aids.Step(Time.fixedDeltaTime,tractionEnabled,stabilityBrakes,traction,yaw);
  requested=Mathf.Max(requested,aidPressure/capacity);
  float scale=(float)abs.Step(Time.fixedDeltaTime,controls!=null&&controls.absEnabled,controller.IsGrounded,controller.currentState.localWheelVelocity.y,omega*controller.WheelRadius,requested);
  controller.brakeInput=requested*scale;
  controller.brakeResponse=aidPressure>0||controls!=null&&controls.absEnabled&&service>0?100:service>0?brakeResponse:8;
  // A separate hydraulic-style handbrake locks only the rear axle. Apply
  // after ABS so the intentional lock is not released by normal anti-lock.
  if(brakesWorking&&controls!=null&&controls.HandbrakeActive&&!controls.Front(part)){
   controller.brakeInput=1/capacity;controller.brakeResponse=100;
  }
 }
}
