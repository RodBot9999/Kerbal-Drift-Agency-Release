using System;
using KerbalDriftAgency;
class ImportedBoostTests {
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static EngineCore Engine(double spool){return new EngineCore {IdleRpm=1400,LimitRpm=10600,LaunchRpm=1400,GearRatio=1,FinalDrive=1,Rpm=5000,RpmResponseSeconds=.01,MaxBoost=.77,BoostTorqueGain=1,BoostFullRpm=1900,SpoolSeconds=spool,ImportedBoostMap=true,ImportedPeakBoost=.77,BoostRpm=new double[]{0,1000,1900,10600},BoostGauge=new double[]{0,.14,.77,.77},CurveRpm=new double[]{0,5000,10600},CurveTorque=new double[]{0,418.36/1.77,180}};}
 static void Main(){
  var fast=Engine(.2);var slow=Engine(1.2);
  for(int i=0;i<20;i++){fast.Step(.02,true,1,5000);slow.Step(.02,true,1,5000);}
  Check(fast.Boost>slow.Boost+.15,"Turbo spool inertia should affect boost delivery");
  for(int i=0;i<500;i++)slow.Step(.02,true,1,5000);
  Check(Math.Abs(slow.Boost-.77)<.001,"Steady boost map");Check(Math.Abs(slow.AvailableTorqueNm-418.36)<.1,"Boost must not be added twice");
  slow.Shifting=true;for(int i=0;i<6;i++)slow.Step(.02,true,1,5000);Check(slow.Boost<.7&&slow.Boost>.1,"Shift pressure release");
  slow.Shifting=false;for(int i=0;i<100;i++)slow.Step(.02,true,1,5000);Check(slow.Boost>.74,"Post-shift recovery");
  for(int i=0;i<30;i++)slow.Step(.02,true,0,5000);Check(slow.Boost<.2,"Lift pressure release");
  var controller=Engine(.2);controller.MaxBoost=.385;for(int i=0;i<500;i++)controller.Step(.02,true,1,5000);Check(Math.Abs(controller.Boost-.385)<.001,"Proportional boost controller");
  var stock=new EngineCore();stock.Step(.02,true,1,0);Check(!stock.ImportedBoostMap,"default calibration unaffected");
  Console.WriteLine("PASS: imported spool, no double boost, shift/lift release, recovery, controller and legacy defaults");
 }
}
