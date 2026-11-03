using System;using ModuleWheels;using KerbalDriftAgency.ECU;
static class AddonTests {
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static bool Near(double a,double b){return Math.Abs(a-b)<1e-5;}
 public static void Run(){
  Check(AddonProfile.Mode(0)==0&&AddonProfile.Mode(1)==2,"Road/Race mapping confined to ECU");
  Check(AddonProfile.Tolerance(1)==5,"Normal uses fixed five-degree tolerance");
  var pitch=new AddonPitchGuard();
  for(int i=0;i<50;i++)pitch.Step(.02,1,.8,25,10,.6,.01,.5,false,true);
  Check(pitch.Scale==1&&!pitch.Triggered,"Normal does NOT anticipate unloading, even with severe pitch/low rear load");
  pitch.Step(.02,1,.8,25,4,.2,0,.5,true,true);Check(pitch.Scale<1&&pitch.Triggered,"Normal reacts after actual rear unloading");
  for(int i=0;i<50;i++)pitch.Step(.02,1,.8,25,12,1,0,.5,true,true);
  Check(pitch.Scale>=.599&&pitch.Scale<.75,"Normal limited relief still permits a flip");
  for(int i=0;i<250;i++)pitch.Step(.02,1,.8,25,0,0,.5,.5,false,true);
  Check(pitch.Scale>.999,"Normal recovers brake pressure after recontact");
  pitch.Reset();for(int i=0;i<20;i++)pitch.Step(.02,0,.8,25,3,.25,.20,.5,false,true);
  Check(pitch.Scale<.6&&pitch.Triggered,"Unstable intervenes while rear wheels retain positive load");
  pitch.Step(.02,0,0,25,15,1,0,.5,true,true);Check(pitch.Scale==1,"no brake intervention without driver brakes");
  pitch.Step(.02,1,.8,25,0,0,0,.5,true,true);Check(pitch.Scale==1,"isolated rear lift without nose-down evidence ignored");
  pitch.Step(.02,0,.8,-25,15,1,0,.5,true,true);Check(pitch.Scale==1,"reverse bypass");
  pitch.Step(.02,1,.8,25,15,1,0,.5,true,false);Check(pitch.Scale==1,"airborne/front-contact gate");
  var steering=new AddonSteering();double command=0;
  foreach(int profile in new[]{0,1}){
   double previous=0;
   foreach(double percent in new[]{60.0,100.0,160.0}){
    steering.Reset();double budget=EcuCore.Budget(percent,1,10);
    for(int i=0;i<200;i++)command=steering.Step(.02,profile,true,false,1,1,25,25,2,9.81,0,0,0,0,budget,true);
    Check(command>previous,"allowance raises actual steering limit even for high-CG rover/profile "+profile);previous=command;
   }
  }
  foreach(int profile in new[]{0,1}){
   steering.Reset();double largest=0;
   for(int i=0;i<500;i++){command=steering.Step(.02,profile,true,true,1,0,30,25,2,9.81,Math.Sin(i*.3),.02*Math.Cos(i*.2),0,.02,.65,true);largest=Math.Max(largest,Math.Abs(command));}
   Check(largest<1e-8,"straight-line noise does not command autonomous steering in profile "+profile);
   for(int i=0;i<100;i++)command=steering.Step(.02,profile,true,true,1,0,20,25,2,9.81,25,.7,0,0,.65,true);
   for(int i=0;i<250;i++)command=steering.Step(.02,profile,true,true,1,0,30,25,2,9.81,0,0,0,0,.65,true);
   Check(Math.Abs(command)<.0005,"recovery returns to centre after slide in profile "+profile);
  }
  steering.Reset();
  for(int i=0;i<150;i++)command=steering.Step(.02,1,false,true,1,0,20,25,2,9.81,4.9,0,0,0,.65,true);
  Check(Near(steering.Correction,0)&&Near(command,0),"Normal accepts 4.9-degree steady slide with no yaw excess");
  for(int i=0;i<100;i++)command=steering.Step(.02,1,false,true,1,0,20,25,2,9.81,5,0,0,0,.65,true);
  Check(Near(steering.Correction,0),"Normal accepts exactly five degrees");
  for(int i=0;i<150;i++)command=steering.Step(.02,1,false,true,1,0,20,25,2,9.81,9,0,0,0,.65,true);
  Check(Math.Abs(steering.Correction)>.01,"Normal corrects excess oversteer");
  Check(AddonProfile.Normalize(2)==1&&AddonProfile.Mode(2)==2&&AddonProfile.Tolerance(2)==5,"old removed profile maps to Normal");
  Check(AddonProfile.Normalize(-1)==0&&AddonProfile.Normalize(99)==1,"invalid profiles constrained to supported choices");
  var vessel=new Vessel();FlightGlobals.ActiveVessel=vessel;var p=new Part{vessel=vessel};var b=p.Add<ModuleWheelBase>();var s=p.Add<ModuleWheelSteering>();var motor=p.Add<ModuleWheelMotor>();p.Add<ModuleWheelSuspension>();p.Add<ModuleWheelBrakes>();
  var owner=new KDAECU{vessel=vessel,addonProfile=1};owner.Choice(p).Group=1;var lease=new EcuWheel(owner,b);lease.Activate(true);
  Check(s.angleTweakable==25&&s.steeringRange==25&&b.Wheel.maxSteerAngle==25,"ECU retains original steering lock");
  var saved=new ConfigNode();s.Save(saved);Check(saved.GetValue("angleTweakable")=="25"&&saved.GetValue("steeringRange")=="25","saved steering lock remains native");lease.Restore();
  owner.addonProfile=1;owner.TestAddonScale=.5f;lease=new EcuWheel(owner,b);lease.Activate(true);b.Wheel.currentState.localWheelVelocity=new UnityEngine.Vector2(0,20);b.Wheel.currentState.angularVelocity=20/.3f;b.Wheel.brakeInput=.8f;
  var frame=lease.CaptureFrame();lease.Before(frame);Check(Near(b.Wheel.brakeInput,.4),"front pitch relief reaches native physics brake input");Check(Near(b.Wheel.maxDriveTorque,.12)&&motor.motorEnabled,"pitch guard never alters native powertrain");lease.After(frame);Check(Near(b.Wheel.brakeInput,.8),"native brake request restored after physics");lease.Restore();
  Console.WriteLine("PASS "+checks+" ECU profile/pitch checks");
 }
}
