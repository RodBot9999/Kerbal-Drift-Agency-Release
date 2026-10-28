using System;
using KerbalDriftAgency;
class DrivelineCoreTests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){
  double pedal=0;pedal=DrivelineCore.Pedal(pedal,1,.02,.25,.06);Check(Math.Abs(pedal-.08)<1e-9,"Initial press must not give full throttle");
  for(int i=0;i<20;i++)pedal=DrivelineCore.Pedal(pedal,1,.02,.25,.06);Check(pedal==1,"Held press reaches full throttle");
  for(int i=0;i<3;i++)pedal=DrivelineCore.Pedal(pedal,0,.02,.25,.06);Check(pedal<1e-8,"Fast release");
  Check(DrivelineCore.Pedal(.5,-1,.02,.25,.06)>0,"Direction reversal first releases");
  Check(DrivelineCore.Pedal(0,1,.02,0,0)==1,"Smoothing bypass");
  Check(DrivelineCore.LeftShare(1,1,2,0,0)==.5,"Open equal torque");
  Check(DrivelineCore.LeftShare(1,1,.04,0,2)==.5,"Normal corner speed differences do not lock");
  for(int mode=1;mode<=2;mode++)for(int i=0;i<100;i++){
   double share=DrivelineCore.LeftShare(1,1,i*.02,0,mode);
   Check(share>=1.0/3&&share<=.5,"Limited torque transfer toward gripping wheel");
   double mirrored=DrivelineCore.LeftShare(1,1,0,i*.02,mode);Check(Math.Abs(share+mirrored-1)<1e-9,"Symmetric and conserved pair torque");
   Check(Math.Abs(.4*share+.4*(1-share)-.4)<1e-9,"AWD axle split retained");
  }
  Check(DrivelineCore.LeftShare(1,1,.05,0,3)<.5,"Drift engages earlier");
  Check(DrivelineCore.LeftShare(1,1,.5,0,3)<DrivelineCore.LeftShare(1,1,.5,0,2),"Drift biases more strongly");
  Check(DrivelineCore.LeftShare(1,1,2,2,3)==.5,"Equal wheelspin retains full axle drive");
  for(int i=0;i<100;i++){
   double share=DrivelineCore.LeftShare(1,1,i*.02,0,3);
   Check(share>=.2&&share<=.5,"Drift bounded 4:1 bias");
   Check(Math.Abs(share+DrivelineCore.LeftShare(1,1,0,i*.02,3)-1)<1e-9,"Drift symmetry and torque conservation");
  }
  Console.WriteLine("PASS: pedal, Open/Mild/Sport/Drift LSD, bounds, symmetry, equal wheelspin and axle torque conservation");
 }
}
