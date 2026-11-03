using System;
using KerbalDriftAgency;
class AwdCorneringTests {
 static int checks;
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;}
 static void Near(double a,double b,string text){Check(Math.Abs(a-b)<1e-7,text+" actual="+a+" expected="+b);}
 static double Step(AwdCorneringCore c,int mode=2,bool eligible=true,double input=1,double baseline=.016,double goal=-.356,double yaw=-.143,double yawRate=0,double speed=59.5,double slip=.22,double rate=0,double allowed=17,double unload=.34,double roll=.6,double frontSide=.48,double rearSide=.26,double frontSpin=0,double rearSpin=0,double assist=1,double dt=.02){return c.Step(dt,eligible,mode,assist,input,baseline,goal,yaw,yawRate,speed,2.2,40,slip,rate,allowed,unload,roll,frontSide,rearSide,frontSpin,rearSpin);}
 static double Hold(int mode,double spin=0){var c=new AwdCorneringCore();double output=0;for(int i=0;i<100;i++)output=Step(c,mode,frontSpin:spin);return output;}
 static DrivelineWheelState Wheel(){return new DrivelineWheelState{Weight=.25,Grounded=true,Slip=0,ShaftSpeed=100,ExpectedShaftSpeed=100};}
 static void Main(){
  var c=new AwdCorneringCore();double first=Step(c);Check(first>.016&&first<.0175,"Progressive first powered correction");double last=0;for(int i=0;i<100;i++)last=Step(c);Check(last>.025&&last<=.0296,"Stable yaw deficit receives bounded extra turn-in");
  var mirror=new AwdCorneringCore();double mirrored=0;for(int i=0;i<100;i++)mirrored=Step(mirror,input:-1,baseline:-.016,goal:.356,yaw:.143,slip:-.22);Near(last,-mirrored,"Mirrored cornering signs");
  Check(Hold(2)>Hold(1)&&Hold(1)>Hold(0)&&Hold(0)>.016,"Sport and Road derive smaller authority from same law");
  foreach(int mode in new[]{0,1,2}){
   c=new AwdCorneringCore();for(int i=0;i<50;i++)Step(c,mode,frontSpin:.25);Check(c.RearAssist>0,"Front spin requests center assistance in each mode");
   Near(Step(c,mode,eligible:false),.016,"Lift/brake/HB/airborne/RWD eligibility resets immediately");Near(c.AddedSteering+c.RearAssist+c.YawDeficit+c.Support,0,"No stored powered correction after gate");
   for(int i=0;i<50;i++)Step(c,mode);Near(Step(c,3),.016,"Drift is excluded");
  }
  c=new AwdCorneringCore();for(int i=0;i<80;i++)Step(c);Near(Step(c,input:0,baseline:0,goal:0),0,"Key release returns original steering immediately");
  c=new AwdCorneringCore();for(int i=0;i<80;i++)Step(c);double reverse=Step(c,input:-1,baseline:-.016,goal:.356,yaw:.143,slip:-.22);Check(reverse<-.016&&reverse>-.0175,"Direction change cannot retain opposite trim");
  c=new AwdCorneringCore();for(int i=0;i<80;i++)Step(c);double modeChange=Step(c,mode:0);Check(modeChange>.016&&modeChange<.017,"Mode change resets Race authority before Road application");
  c=new AwdCorneringCore();for(int i=0;i<100;i++)last=Step(c,yaw:-.4);Near(last,.016,"No extra angle when achieved yaw exceeds target");
  c=new AwdCorneringCore();for(int i=0;i<100;i++)last=Step(c,yawRate:-3);Near(last,.016,"Predictive yaw removes correction before overshoot");
  c=new AwdCorneringCore();for(int i=0;i<100;i++)last=Step(c,yaw:.2);Near(last,.016,"Opposite yaw stays with recovery");
  foreach(int mode in new[]{0,1,2})foreach(double bad in new[]{5.0,20.0,90.0}){c=new AwdCorneringCore();for(int i=0;i<100;i++)last=Step(c,mode,slip:bad,frontSpin:.3);Near(last,.016,"No added angle in rear slide/spin");Near(c.RearAssist,0,"No extra rear bias during slide");}
  foreach(int mode in new[]{0,1,2}){c=new AwdCorneringCore();for(int i=0;i<100;i++)last=Step(c,mode,slip:.6,rate:40);Near(last,.016,"Growing rear slip preempts turn-in");}
  foreach(double u in new[]{.85,1.0}){c=new AwdCorneringCore();for(int i=0;i<100;i++)last=Step(c,unload:u);Near(last,.016,"Wheel unloading blocks added steering");}
  c=new AwdCorneringCore();for(int i=0;i<100;i++)last=Step(c,frontSide:1.3);Near(last,.016,"Saturated front lateral slip blocks turn-in");
  c=new AwdCorneringCore();for(int i=0;i<100;i++)last=Step(c,rearSide:1.1);Near(last,.016,"Rear lateral saturation blocks turn-in");
  Check(Hold(2,.3)<Hold(2),"Front drive spin reduces additional steering");
  c=new AwdCorneringCore();for(int i=0;i<100;i++)last=Step(c,frontSpin:.5);Near(last,.016,"Severe front wheelspin blocks added steering");Check(c.RearAssist>0&&c.RearAssist<=.12,"Active center can relieve front saturation without adding angle");
  c=new AwdCorneringCore();for(int i=0;i<100;i++)Step(c,rearSpin:.4);Near(c.AddedSteering,0,"Rear drive spin blocks added turn-in");Near(c.RearAssist,0,"Rear spin does not request more rear power");
  c=new AwdCorneringCore();for(int i=0;i<100;i++)Step(c,frontSpin:.7,rearSpin:.4);Near(c.RearAssist,0,"Both axles spinning cannot add a rear cornering bias");
  c=new AwdCorneringCore();for(int i=0;i<100;i++)Step(c);Near(Step(c,speed:-30),.016,"Reverse excluded");Near(Step(c,speed:5),.016,"Low speed excluded");Near(Step(c,assist:0),.016,"SC off excluded");Near(Step(c,speed:Double.NaN),.016,"Nonfinite input fails closed");Near(Step(c,baseline:Double.NaN),0,"Nonfinite output sanitized");
  var cfg=new ActiveDrivelineConfig{Center=true,CenterOffset=.05,CenterRate=1.5,PoweredRearMinimum=.62};var drive=new ActiveDrivelineCore();ActiveDriveAllocation a=new ActiveDriveAllocation();for(int i=0;i<50;i++)a=drive.Step(.02,cfg,Wheel(),Wheel(),Wheel(),Wheel(),0,0,true,true);Near(a.RearShare,.55,"Rear assist respects exported center bias bound");
  cfg.Center=false;drive.Reset();a=drive.Step(.02,cfg,Wheel(),Wheel(),Wheel(),Wheel(),0,0,true,true);Near(a.RearShare,.5,"Passive AWD does not gain an active center");
  cfg.Center=true;drive.Reset();a=drive.Step(.02,cfg,Wheel(),Wheel(),Wheel(),Wheel(),0,0,false,false);Near(a.RearShare,.5,"Coast allocation unchanged despite stale request");
  // A simple yaw-lag plant exercises feedback sign, bounds and no windup. It is
  // not a replacement for the native KSP handling check.
  foreach(int mode in new[]{0,1,2}){c=new AwdCorneringCore();double yaw=0,angle=.016,peak=0;for(int i=0;i<1500;i++){angle=Step(c,mode,yaw:yaw);double target=-angle*40*Math.PI/180*59.5/2.2*.55;yaw+=(target-yaw)*(1-Math.Exp(-.02/.20));Check(angle>=.016&&angle<=.0296,"Closed-loop simulated steering bounded");peak=Math.Max(peak,Math.Abs(yaw));}Check(Math.Abs(yaw)>.16&&peak<.36,"Simulated yaw improves without overshoot");}
  var random=new Random(1610);for(int i=0;i<5000;i++){int mode=i%3;c=new AwdCorneringCore();double raw=random.NextDouble()*2-1,baseline=raw*.05;double output=Step(c,mode,input:raw,baseline:baseline,goal:-raw*.5,yaw:random.NextDouble()-.5,slip:random.NextDouble()*30-15,unload:random.NextDouble(),frontSpin:random.NextDouble(),rearSpin:random.NextDouble());Check(!Double.IsNaN(output)&&Math.Abs(output)<=1&&c.RearAssist>=0&&c.RearAssist<=.12,"Randomized bounds and valid allocation request");}
  Console.WriteLine("PASS: "+checks+" AWD powered Race/Sport/Road cornering, transition, hardware and stability checks");
 }
}
