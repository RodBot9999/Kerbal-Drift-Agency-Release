using System;
class RecoverySteeringTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Main(){
  for(int mode=0;mode<4;mode++){
   double angle=mode==3?60:40,tolerance=mode==3?12:5;
   var slow=KDACore.Evaluate(mode,1,tolerance,15,0,angle,2.7,9.81,18,0,0,0,true,false);
   var fast=KDACore.Evaluate(mode,1,tolerance,60,0,angle,2.7,9.81,18,0,0,0,true,false);
   var rolling=KDACore.Evaluate(mode,1,tolerance,60,0,angle,2.7,9.81,18,0,15,.8,true,false);
   Check(fast.Limit<slow.Limit,"Turn-in retains speed protection");
   Check(fast.RecoveryLimit==slow.RecoveryLimit&&rolling.RecoveryLimit==fast.RecoveryLimit,"Recovery lock not reduced by speed/unloading");
   Check(fast.Correction<0&&Math.Abs(fast.Correction)*angle>12,"Slide receives useful corrective angle");
   Check(KDACore.DriverLimit(-1,18,0,true,fast)*angle==(mode==3?50:mode==2?32:25),"Driver recovery reaches mode cap");
   Check(KDACore.DriverLimit(1,18,0,true,fast)==fast.Limit,"Turning further into slide remains limited");
   var straight=KDACore.Evaluate(mode,1,tolerance,60,1,angle,2.7,9.81,0,0,0,0,true,false);
   Check(straight.Correction==0&&straight.RecoveryLimit==straight.Limit,"Straight driving has no extra lock");
  }
  foreach(double degrees in new[]{25.0,50.0,75.0}){
   double physical=Math.Max(60,degrees);
   double cap=KDACore.RecoveryLock(3,physical,1,degrees);
   Check(Math.Abs(cap*physical-degrees)<1e-9,"Selected drift lock reaches requested physical angle");
   var slide=KDACore.Evaluate(3,1,12,60,0,physical,2.7,9.81,40,0,0,0,true,false,1,degrees);
   Check(Math.Abs(slide.RecoveryLimit-cap)<1e-9,"Automatic and manual recovery share selected cap");
   Check(KDACore.RecoveryLock(2,40,1,degrees)==.8,"Drift slider does not change Race lock");
  }
  Check(KDACore.DriftLock(999)==75&&KDACore.DriftLock(-1)==25&&KDACore.DriftLock(Double.NaN)==50,"Invalid and out-of-range settings bounded");
  Console.WriteLine("PASS: separate high-speed turn-in, adjustable drift lock and fixed Race cap");
 }
}
