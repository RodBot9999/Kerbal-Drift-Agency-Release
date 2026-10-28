// SPDX-License-Identifier: MIT
using System;using System.Reflection;using ModuleWheels;using KerbalDriftAgency.ECU;
class AdapterTests {
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}static bool Near(double a,double b){return Math.Abs(a-b)<1e-5;}
 static Part Wheel(Vessel v){var p=new Part{vessel=v};p.Add<ModuleWheelBase>();p.Add<ModuleWheelSteering>();p.Add<ModuleWheelMotor>();p.Add<ModuleWheelSuspension>();p.Add<ModuleWheelBrakes>();return p;}
 static void NativeUnchanged(ModuleWheelBase b,ModuleWheelMotor m,ModuleWheelSuspension s){Check(m.motorEnabled&&!m.motorInverted,"native motor switches unchanged");Check(b.autoFriction&&Near(b.frictionPeak,1.2)&&Near(b.frictionMultiplier,1.4),"native grip unchanged");Check(s.autoSpringDamper&&Near(s.suspensionDistance,.2),"native suspension config unchanged");Check(Near(b.Wheel.maxDriveTorque,.12)&&Near(b.Wheel.maxRpm,900)&&Near(b.Wheel.driveInput,.8),"native drive input/caps unchanged");Check(Near(b.Wheel.wheelCollider.springRate,20)&&Near(b.Wheel.wheelCollider.damperRate,2)&&Near(b.Wheel.wheelCollider.suspensionDistance,.2),"native physical suspension unchanged");}
 static void Main(){
  for(int n=4;n<=12;n+=2){var points=new WheelPoint[n];for(int i=0;i<n;i++)points[i]=new WheelPoint{X=5+(i%2==0?-1:1),Z=7-i/2,CanSteer=true};var pair=EcuCore.FrontPair(points);Check(pair[0]==0&&pair[1]==1,"front pair for offset ECU and multiple axles");points[0].Group=2;Check(EcuCore.FrontPair(points)[0]!=0,"rear group not selected to steer");}
  Check(EcuCore.FrontPair(new[]{new WheelPoint{X=0,Z=0,CanSteer=true}})[0]<0,"missing pair rejected");Check(EcuCore.Budget(100,1,2)<.2,"roll budget for high CG");
  foreach(double height in new[]{.4,2.0,10.0}){
   double baseline=EcuCore.Budget(100,1,height);
   Check(Near(EcuCore.Budget(60,1,height),baseline*.6),"lower allowance scales calculated baseline");
   Check(Near(EcuCore.Budget(160,1,height),baseline*1.6),"higher allowance is not clipped by baseline");
   Check(Near(EcuCore.Budget(double.NaN,1,height),baseline),"invalid allowance falls back to 100 percent");
  }
  var owner=new KDAECU{vessel=new Vessel()};FlightGlobals.ActiveVessel=owner.vessel;var p=Wheel(owner.vessel);var b=p.FindModuleImplementing<ModuleWheelBase>();var s=p.FindModuleImplementing<ModuleWheelSteering>();var motor=p.FindModuleImplementing<ModuleWheelMotor>();var sus=p.FindModuleImplementing<ModuleWheelSuspension>();var lease=new EcuWheel(owner,b);lease.Activate(false);Check(!s.steeringEnabled,"extra steering disabled");var saved=new ConfigNode();s.Save(saved);Check(saved.GetValue("steeringEnabled")=="True","native steering saved while disabled by ECU");
  b.Wheel.currentState.localWheelVelocity=new UnityEngine.Vector2(0,20);b.Wheel.currentState.angularVelocity=0;
  for(int i=0;i<20;i++){var frame=lease.CaptureFrame();lease.Before(frame);Check(b.Wheel.brakeInput<.7,"ABS releases a locked wheel");NativeUnchanged(b,motor,sus);lease.After(frame);Check(Near(b.Wheel.brakeInput,.7)&&Near(b.Wheel.brakeResponse,2),"native brake requests restored after physics");}
  lease.Restore();Check(s.steeringEnabled&&s.autoSteeringAdjust&&Near(s.responseTweakable,1),"OFF restores steering");NativeUnchanged(b,motor,sus);Check(EcuHooks.Wheels.Count==0,"wheel registry released");
  foreach(int profile in new[]{0,1}){owner.addonProfile=profile;lease=new EcuWheel(owner,b);lease.Activate(true);for(int i=0;i<5;i++){var frame=lease.CaptureFrame();lease.Before(frame);NativeUnchanged(b,motor,sus);lease.After(frame);}lease.Restore();NativeUnchanged(b,motor,sus);}
  s.steeringEnabled=false;lease=new EcuWheel(owner,b);lease.Activate(true);lease.Restore();Check(!s.steeringEnabled,"original disabled steering restored too");
  var other=Wheel(new Vessel());lease=new EcuWheel(owner,other.FindModuleImplementing<ModuleWheelBase>());lease.Activate(true);Check(!lease.CaptureFrame().Applied,"unrelated vessel ignored");lease.Restore();Check(EcuHooks.Wheels.Count==0,"no registry leaks");
  // Simulate the conflicting stock per-wheel geometry immediately before
  // VPP OnUpdate. The owned outputs must agree physically, including mirrored
  // wheel axes, differing native lock ranges and rear keyboard steering.
  owner.Active=owner.ecuEnabled=true;s.steeringEnabled=true;
  var p2=Wheel(owner.vessel);var b2=p2.FindModuleImplementing<ModuleWheelBase>();
  b2.Wheel.Orientation=-1;b2.Wheel.maxSteerAngle=40;
  foreach(int profile in new[]{0,1}){
   owner.addonProfile=profile;lease=new EcuWheel(owner,b);var pairLease=new EcuWheel(owner,b2);lease.Activate(true);pairLease.Activate(true);
   foreach(float degrees in new[]{0f,12f,-12f,0f}){
    owner.SteeringDegrees=degrees;b.Wheel.steerInput=.9f;b2.Wheel.steerInput=-.7f;b.Wheel.keyboardSteering=b2.Wheel.keyboardSteering=true;
    lease.ApplySteering();pairLease.ApplySteering();
    Check(Near(-b.Wheel.steerInput*b.Wheel.maxSteerAngle,degrees),"left physical angle follows shared command");
    Check(Near(b2.Wheel.steerInput*b2.Wheel.maxSteerAngle,degrees),"mirrored right physical angle follows shared command");
    Check(!b.Wheel.keyboardSteering&&!b2.Wheel.keyboardSteering,"raw keyboard cannot overwrite ECU steering");
   }
   NativeUnchanged(b,motor,sus);lease.Restore();pairLease.Restore();
   Check(b.Wheel.keyboardSteering&&Near(b.Wheel.steeringResponse,2),"OFF restores native steering keyboard and response");
  }
  owner.addonProfile=1;lease=new EcuWheel(owner,b);lease.Activate(false);b.Wheel.steerState=.6f;b.Wheel.steerInput=.8f;lease.ApplySteering();
  Check(b.Wheel.steerInput==0&&b.Wheel.steerState==0&&!b.Wheel.keyboardSteering,"rear wheels centred despite stock/key input");lease.Restore();
  lease=new EcuWheel(owner,b);lease.Activate(true);b.Wheel.Orientation=0;b.Wheel.steerState=.4f;lease.ApplySteering();Check(b.Wheel.steerInput==0&&b.Wheel.steerState==0,"invalid orientation fails centred");b.Wheel.Orientation=1;lease.Restore();
  lease=new EcuWheel(owner,b);lease.Activate(true);FlightGlobals.ActiveVessel=new Vessel();b.Wheel.steerInput=.4f;lease.ApplySteering();Check(Near(b.Wheel.steerInput,.4),"inactive vessel steering not overwritten");lease.Restore();FlightGlobals.ActiveVessel=owner.vessel;
  b.Wheel.keyboardSteering=false;b.Wheel.steeringResponse=7;lease=new EcuWheel(owner,b);lease.Activate(true);lease.ApplySteering();lease.Restore();Check(!b.Wheel.keyboardSteering&&Near(b.Wheel.steeringResponse,7),"OFF also preserves originally disabled keyboard steering and custom servo");
  AddonTests.Run();Console.WriteLine("PASS "+checks+" rover-only adapter/geometry checks (managed doubles; native driving still pending)");
 }
}
