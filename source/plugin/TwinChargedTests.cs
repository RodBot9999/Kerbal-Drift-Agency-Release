using System;
using KerbalDriftAgency;
class TwinChargedTests {
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
 static EngineCore Engine(double rpm,bool twin=true){return new EngineCore {
  IdleRpm=1300,LimitRpm=8000,LaunchRpm=1300,GearRatio=1,FinalDrive=1,
  Rpm=rpm,RpmResponseSeconds=.01,MaxBoost=2.7,BoostTorqueGain=1,
  BoostFullRpm=1900,SpoolSeconds=.68,ImportedBoostMap=true,ImportedPeakBoost=2.7,
  TwinCharged=twin,MechanicalMaxBoost=2.628970954677,MechanicalDisengageRpm=3400,
  BoostRpm=new double[]{0,1300,1900,3400,8000},BoostGauge=new double[]{0,1.17,2.68,2.7,2.7},
  CurveRpm=new double[]{0,1300,8000},CurveTorque=new double[]{0,200,200}
 };}
 static void Main(){
  var twin=Engine(1500);var turbo=Engine(1500,false);
  for(int i=0;i<20;i++){twin.Step(.02,true,1,1500);turbo.Step(.02,true,1,1500);}
  Check(twin.Boost>turbo.Boost+.4,"Mechanical low-RPM response must precede turbo spool");
  var high=Engine(5000);var highTurbo=Engine(5000,false);
  for(int i=0;i<20;i++){high.Step(.02,true,1,5000);highTurbo.Step(.02,true,1,5000);}
  Check(Math.Abs(high.Boost-highTurbo.Boost)<1e-10,"Above clutch cutoff, boost must follow turbo only");
  foreach(double rpm in new double[]{1500,3000,3200,3400,5000,7900}){
   var e=Engine(rpm);for(int i=0;i<600;i++)e.Step(.02,true,1,rpm);
   double gauge=e.EvaluateBoost(rpm);
   Check(Math.Abs(e.Boost-gauge)<1e-5,"Combined steady pressure must match export across handoff");
   Check(Math.Abs(e.AvailableTorqueNm-200*(1+gauge))<.002,"Boost must not be multiplied twice");
  }
  var reduced=Engine(1500);reduced.MaxBoost=1.35;for(int i=0;i<600;i++)reduced.Step(.02,true,1,1500);
  Check(Math.Abs(reduced.Boost-reduced.EvaluateBoost(1500)*.5)<1e-5,"Boost controller must scale both stages");
  for(int i=0;i<100;i++)twin.Step(.02,true,0,1500);
  Check(twin.Boost<.01,"Throttle lift must release mechanical/turbo manifold pressure");
  var mechanical=Engine(1500,false);mechanical.MechanicalBoost=true;for(int i=0;i<600;i++)mechanical.Step(.02,true,1,1500);
  Check(Math.Abs(mechanical.Boost-mechanical.EvaluateBoost(1500))<1e-5,"Pure imported supercharger uses its original map");
  Console.WriteLine("PASS: twincharged response, clutch cutoff, steady torque, boost scaling, lift and pure supercharger");
 }
}
