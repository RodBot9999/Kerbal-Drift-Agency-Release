using System;
class RoadSportTests {
 static int n;static void Check(bool ok,string name){if(!ok)throw new Exception(name);n++;}
 static void Main(){
  Check(RoadSportCore.Tolerance(0,5)==1.5&&RoadSportCore.Tolerance(1,5)==3,"Road and Sport use smaller slide allowance");
  Check(RoadSportCore.ThrottleRise(0,true,.25)==.55&&RoadSportCore.ThrottleRise(1,true,.25)==.35,"Softer mode pedal response");
  Check(RoadSportCore.PowerCap(0,true,true)==.60,"Road torque ceiling with TCS");
  Check(RoadSportCore.PowerCap(0,true,false)==1&&RoadSportCore.PowerCap(0,false,true)==1,"Disabling TCS or SC bypasses ceiling");
  Check(RoadSportCore.PowerCap(1,true,true)==1,"Sport keeps full power");
  for(int mode=2;mode<4;mode++){
   Check(!RoadSportCore.Applies(mode),"Race/Drift profile excluded");
   Check(RoadSportCore.PowerCap(mode,true,true)==1,"Protected modes full configured power");
   Check(RoadSportCore.ThrottleRise(mode,true,.123)==.123,"Protected modes original pedal response");
   Check(RoadSportCore.PedalInput(-.9,.2,mode,true)==-.9,"Protected modes unmodified motor input");
   foreach(double slip in new[]{0.0,.03,.15,.6,3.0})Check(RoadSportCore.TractionTarget(slip,true,mode)==DriverAidsCore.Traction(slip,true,mode),"Protected modes legacy torque target");
   Check(RoadSportCore.TractionBrake(20,24,true,mode)==DriverAidsCore.TractionBrake(20,24,true,mode),"Protected modes legacy traction brakes");
  }
  foreach(int mode in new[]{0,1})foreach(double speed in new[]{4.0,8.0,20.0,50.0,90.0})foreach(double capacity in new[]{.6,1.0,2.5}){
   var modern=RoadSportCore.Evaluate(mode,1,RoadSportCore.Tolerance(mode,5),speed,1,40,2.65,9.81,0,0,0,0,true,false,capacity);
   var old=KDACore.Evaluate(mode,1,5,speed,1,40,2.65,9.81,0,0,0,0,true,false,capacity);
   Check(Math.Abs(modern.Limit-old.Limit)<1e-12,"Existing Road/Sport cornering-g budget preserved");
   Check(Math.Abs(modern.Correction)<1e-12,"No corrections on straight, steady motion");
   var converging=RoadSportCore.Evaluate(mode,1,1,speed,0,40,2.65,9.81,10,.3,0,0,true,false,capacity,-90,-.01,-2,0);
   Check(Math.Abs(converging.Correction)<.03,"Do not use stale yaw/slip to overshoot recovery");
  }
  Check(Math.Abs(RoadSportCore.PedalInput(1,.1,0,true)-.1)<1e-12,"Actual wheel input follows ramped pedal");
  Check(RoadSportCore.PedalInput(0,.7,0,true)==0,"Lift immediately clears positive wheel input");
  Check(RoadSportCore.PedalInput(-1,-.2,1,true)==-.2,"Reverse direction retained");
  var road=new RoadSportTraction();var sport=new RoadSportTraction();
  for(int i=0;i<50;i++){road.Step(.02,true,.5,true,0);sport.Step(.02,true,.5,true,1);}
  Check(road.Scale<=.031&&sport.Scale>=.5,"Road suppresses spin more strongly than Sport");
  Check(road.Step(.02,false,1,true,0)==1,"TCS off immediately restores wheel torque");
  Check(RoadSportCore.TractionTarget(0,true,0)==1,"Road no needless cut on rolling tires");
  Check(RoadSportCore.TractionBrake(20,20,true,0)==0,"Road rolling tire not braked");
  Check(RoadSportCore.TractionBrake(20,23,true,0)>DriverAidsCore.TractionBrake(20,23,true,0),"Earlier/stronger Road per-wheel traction braking");
  Check(RoadSportCore.TractionBrake(20,25,false,0)==0,"No traction braking in air");
  Console.WriteLine("PASS: "+n+" Road/Sport profile, authority, power and traction checks");
 }
}
