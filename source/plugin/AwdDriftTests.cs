using System;
using KerbalDriftAgency;
class AwdDriftTests {
 static int checks;static void Check(bool ok,string s){if(!ok)throw new Exception(s);checks++;}
 static void Near(double a,double b,string s){Check(Math.Abs(a-b)<1e-7,s+" actual="+a+" expected="+b);}
 static double Hold(int sign,double key,double seconds,out AwdDriftTrim trim){trim=new AwdDriftTrim();double value=0;for(int i=0;i<seconds/.02;i++)value=trim.Step(.02,true,true,key,-sign*53.0/68,-sign*34.45/68,sign*48,33,68,53);return value;}
 static double Taps(int period,int on){var trim=new AwdDriftTrim();for(int i=0;i<100;i++)trim.Step(.02,true,true,i%period<on?1:0,-53.0/68,-34.45/68,48,33,68,53);return trim.AddedSteering;}
 static DrivelineWheelState Wheel(double slip=0,double shaft=100){return new DrivelineWheelState{Weight=.25,Grounded=true,Slip=slip,ShaftSpeed=shaft,ExpectedShaftSpeed=100};}
 static void Main(){
  AwdDriftTrim a,b;double positive=Hold(1,1,1.5,out a),negative=Hold(-1,-1,1.5,out b);
  Check(positive*68>5.8&&positive*68<=6.01,"Held AWD reduction reaches neutral and a small into-slide angle");Near(positive,-negative,"Mirrored AWD steering");
  var first=new AwdDriftTrim();double before=-34.45/68;double now=first.Step(.02,true,true,1,-53.0/68,before,48,33,68,53);Check((now-before)*68<1,"One tap does not replace countersteer instantly");
  Check(Taps(20,1)<Taps(4,1)&&Taps(4,1)<Taps(2,1)&&Taps(2,1)<Taps(1,1),"Tap frequency is progressive");
  double previous=positive;for(int i=0;i<60;i++){now=a.Step(.02,true,true,0,-53.0/68,-34.45/68,48,33,68,53);Check(Math.Abs(now-previous)*68<4,"Release is progressive");previous=now;}Check(Math.Abs(now*68+34.45)<.001,"Released input returns to established balance");
  now=Hold(1,-1,1,out a);Near(now,-34.45/68,"More countersteer receives no opposing AWD trim");
  Near(a.Step(.02,false,true,1,-.7,-.5,48,33,68,53),-.5,"Disabled/RWD path unchanged");
  Near(a.Step(.02,true,false,1,-.7,-.5,48,33,68,53),-.5,"No trim outside a drift");
  Near(a.Step(.02,true,true,1,-.7,-.5,48,0,68,53),-.5,"Reverse/stopped path unchanged");
  a=new AwdDriftTrim();for(int i=0;i<100;i++)now=a.Step(.02,true,true,1,-.7,-.5,90,33,68,53);Near(now,-.5,"No increased rotation in an established spin");
  var clutch=new AwdHandbrakeClutch();clutch.Step(.02,true,true);Near(clutch.Coupling,0,"Handbrake releases rear driveline immediately");
  var split=AwdHandbrakeClutch.Gate(.2,.3,.25,.25,clutch.Coupling);Near(split.FL,.4,"Front split retained under open rear coupling");Near(split.FR,.6,"Existing crank allocation conserved at front");Near(split.RL+split.RR,0,"No rear drive against handbrake");
  Near(AwdHandbrakeClutch.CoupledMean(.5,1000,.5,0,0),1000,"Locked rear wheels cannot halve engine coupling RPM");
  double last=0;for(int i=0;i<14;i++){clutch.Step(.02,true,false);Check(clutch.Coupling>=last&&clutch.Coupling<=1,"Progressive bounded reconnection");last=clutch.Coupling;}Near(clutch.Coupling,1,"Clutch closes after 280ms");
  clutch.Step(.02,true,true);clutch.Step(.02,false,true);Near(clutch.Coupling,1,"RWD/FWD do not gain AWD clutch behavior");
  split=AwdHandbrakeClutch.Gate(0,0,.5,.5,0);Near(split.RL+split.RR,1,"RWD torque unchanged");
  var random=new Random(516);for(int i=0;i<1000;i++){double f=.05+.9*random.NextDouble(),l=random.NextDouble(),r=random.NextDouble();split=AwdHandbrakeClutch.Gate(f*l,f*(1-l),(1-f)*r,(1-f)*(1-r),random.NextDouble());Near(split.FL+split.FR+split.RL+split.RR,1,"Clutch allocation conserves torque");}
  Near(AwdDriftDemand.YawTarget(-.3,1,.8),-.5,"Rotation request assists left yaw");Near(AwdDriftDemand.YawTarget(.3,-1,.8),.5,"Mirrored right yaw");Near(AwdDriftDemand.YawTarget(-.3,0,.8),-.3,"Neutral drift keys do not demand straightening");
  var config=new ActiveDrivelineConfig{Center=true,FrontVectoring=true,RearVectoring=true,CenterOffset=.1,CenterRate=1.5,SpeedThreshold=10,SpeedDifferenceThreshold=1,FrontOffset=.2,RearOffset=.3,FrontRate=1.5,RearRate=1.5,DriftRearMinimum=.75};
  var drive=new ActiveDrivelineCore();ActiveDriveAllocation allocation=new ActiveDriveAllocation();for(int i=0;i<100;i++)allocation=drive.Step(.02,config,Wheel(),Wheel(),Wheel(2,300),Wheel(2,300),0,0,true,true);Near(allocation.RearShare,.6,"Drift rear target respects source center bias limit");
  config.Center=false;drive.Reset();for(int i=0;i<100;i++)allocation=drive.Step(.02,config,Wheel(),Wheel(),Wheel(),Wheel(),0,0,true,true);Near(allocation.RearShare,.5,"No active center program on passive AWD");
  Console.WriteLine("PASS: "+checks+" AWD drift trim, tap/release, source-bound torque/yaw and handbrake clutch checks");
 }
}
