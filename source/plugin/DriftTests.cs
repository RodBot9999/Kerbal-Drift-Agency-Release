using System;
class DriftTests {
 static int n;static void Check(bool v,string s){if(!v)throw new Exception(s);n++;}
 static void Main(){
  var race=KDACore.Evaluate(2,1,8,30,0,40,2.15,9.81,45,0,0,0,true,false);
  var drift=KDACore.Evaluate(3,1,12,30,0,60,2.15,9.81,45,0,0,0,true,false);
  Check(Math.Abs(drift.Steering)*60>Math.Abs(race.Steering)*40,"Drift larger correction");
  Check(drift.RecoveryLimit*60<=50,"Drift bounded lock");
  Check(KDACore.DriverLimit(-1,45,0,true,drift)==drift.RecoveryLimit,"driver recovery authority");
  Check(KDACore.DriverLimit(1,45,0,true,drift)==drift.Limit,"turn into slide limited");
  Check(KDACore.DriverLimit(-1,45,0,false,drift)==drift.Limit,"noise no extra authority");
  Check(KDACore.SteeringTau(.4,-.4,.16,2)<=.045,"Race fast reversal");
  Check(KDACore.SteeringTau(0,1,.16,2)<=.10,"Race fast turn in");
  for(int mode=0;mode<4;mode++){
   var r=KDACore.Evaluate(mode,1,12,60,0,mode==3?60:40,2.15,9.81,.2,.02,0,0,true,false);
   Check(r.Correction==0,"straight noise ignored");
  }
  Check(DriverAidsCore.Traction(.15,true,0)>.7,"Road avoids hard lift during spin");
  Check(DriverAidsCore.Traction(.15,true,1)>.8,"Sport avoids hard lift during spin");
  Check(DriverAidsCore.Traction(.15,true,2)>0,"Race unchanged");
  Check(DriverAidsCore.Traction(1,true,3)==1,"Drift torque unrestricted");
  Check(DriverAidsCore.TractionBrake(20,23,true,0)>0,"spinning wheel brake");
  Check(DriverAidsCore.TractionBrake(20,20,true,0)==0,"rolling wheel unbraked");
  Check(DriverAidsCore.TractionBrake(20,23,true,2)==0,"Race no traction brake");
  Check(DriverAidsCore.TractionBrake(20,23,false,0)==0,"airborne no traction brake");
  Console.WriteLine(n+" drift checks passed");
 }
}
