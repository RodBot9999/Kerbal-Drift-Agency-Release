using System;
class TcsRevisionTests {
 static int n;static void Check(bool v,string s){if(!v)throw new Exception(s);n++;}
 static void Main(){
  for(int mode=0;mode<3;mode++){
   var tc=new TractionCore();double before=tc.Scale;
   for(int i=0;i<100;i++){double now=tc.Step(.02,true,2,true,mode);Check(before-now<=.020001,"no sudden torque lift");before=now;}
   Check(tc.Scale>=(mode==2?.7:mode==1?.45:.35)-1e-9,"torque floor");
   Check(tc.Step(.02,false,2,true,mode)==1,"disabled bypass immediate");
   Check(DriverAidsCore.RollPower(5,mode)==1,"normal slide no global lift");
   Check(DriverAidsCore.YawBrake(.4,-1,true,mode)>0,"right yaw brakes left front");
   Check(DriverAidsCore.YawBrake(-.4,1,true,mode)>0,"left yaw brakes right front");
   Check(DriverAidsCore.YawBrake(.4,1,true,mode)==0,"wrong side unbraked");
   Check(DriverAidsCore.YawBrake(.4,-1,false,mode)==0,"rear yaw brake avoided");
  }
  var drift=new TractionCore();Check(drift.Step(.02,true,4,true,3)==1,"Drift lock bypass");Check(DriverAidsCore.YawBrake(2,-1,true,3)==0,"Drift no yaw brake");Check(DriverAidsCore.RollPower(25,3)==1,"Drift no power cut");
  Check(DriverAidsCore.ExcessYaw(.08,0)==0,"yaw noise ignored");
  var race=KDACore.Evaluate(2,1,8,30,0,40,2.15,9.81,45,0,0,0,true,false);Check(Math.Abs(race.RecoveryLimit*40-32)<1e-9,"Race extra recovery lock");
  Console.WriteLine(n+" TCS revision checks passed");
 }
}


