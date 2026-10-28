using System;
using KerbalDriftAgency;
class TurboRaceTests {
 static void Check(bool c,string message){if(!c)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static double Wheel(EngineCore e,double rpm){return rpm/(e.GearRatio*e.FinalDrive);}
 static void Run(EngineCore e,double seconds,double throttle,double rpm){for(int i=0;i<(int)(seconds/.01);i++)e.Step(.01,true,throttle,Wheel(e,rpm));}
 static EngineCore Big(){var e=new EngineCore{MaxBoost=1.56,BoostStartRpm=3200,BoostFullRpm=3550,SpoolSeconds=.75};InductionCore.Configure(e,3);return e;}
 static void Main(){
  var big=Big();Run(big,5,1,3150);Check(big.Boost<.001&&big.Rotor>.7,"big turbo stores rotor energy below boost threshold without producing boost");
  Run(big,.8,1,3550);Console.WriteLine("big hit RPM="+big.Rpm+" boost="+big.Boost);Check(big.Boost>big.MaxBoost*.88,"pre-spooled big turbo reaches about 90 percent boost within a 400 RPM rise");
  double surge=big.Boost;Run(big,3,1,4000);Check(big.Boost>surge&&big.Boost<=big.MaxBoost,"remaining boost builds after the initial hit without overboost");
  var cold=Big();Run(cold,.4,1,4000);Check(cold.Boost<big.Boost*.6,"cold rotor still has lag above threshold");
  double rotor=big.Rotor,pressure=big.Boost;Run(big,.18,0,4000);Check(big.Rotor>rotor*.80&&big.Boost<pressure*.75,"lift vents manifold pressure faster than rotor energy");
  double lifted=big.Boost;Run(big,.3,1,4000);Check(big.Boost>lifted&&big.Boost>cold.Boost,"quick reapplication recovers from retained rotor energy");
  var loaded=Big();var unloaded=Big();unloaded.EngineLoad=.03;Run(loaded,6,1,4500);Run(unloaded,6,1,4500);Check(unloaded.Boost<loaded.Boost*.05,"airborne/free wheel load cannot build full turbo boost");
  var small=new EngineCore{MaxBoost=.55,BoostStartRpm=1300,BoostFullRpm=2600,SpoolSeconds=.5,LimitRpm=9000};InductionCore.Configure(small,1);Run(small,5,1,4000);double mid=small.Boost;Run(small,5,1,8000);Check(small.Boost<mid*.7,"small turbo boost tapers at high RPM");
  var sc=new EngineCore{MaxBoost=.55,BoostStartRpm=900,BoostFullRpm=1200,GearRatio=0};InductionCore.Configure(sc,4);
  for(int i=0;i<200;i++)sc.Step(.01,true,1,0);Check(sc.Boost>.5&&sc.Rotor==0,"mechanical supercharger responds in neutral without turbo rotor lag");
  var race=new EngineCore{RaceLimiter=true};Run(race,6,1,6510);double boost=race.Boost;int cuts=0,resumes=0;bool prev=race.Limiter;double min=10000,max=0;
  for(int i=0;i<200;i++){race.Step(.01,true,1,Wheel(race,6510));if(race.Limiter&&!prev)cuts++;if(!race.Limiter&&prev)resumes++;prev=race.Limiter;min=Math.Min(min,race.Rpm);max=Math.Max(max,race.Rpm);}
  Check(cuts>=14&&cuts<=18&&resumes>=14,"race limiter pulses around eight times per second");
  Check(min>race.LimitRpm-120&&max<race.LimitRpm+100,"race limiter keeps RPM in a narrow band");
  Check(race.Boost>boost*.9,"short race cuts preserve boost");
  Run(race,1,1,7000);Check(race.Limiter&&race.OutputTorqueNm==0,"severe overspeed still forces continuous protection");
  Run(race,1,0,5000);Check(!race.LimiterActive&&race.Boost<.08,"lift and lower RPM clear the race limiter and vent boost");
  var surgeGuard=new EngineCore{RaceLimiter=true,Rpm=3000};surgeGuard.Step(.01,true,1,Wheel(surgeGuard,6600));
  Check(surgeGuard.LimiterActive&&surgeGuard.AvailableTorqueNm==0,"actual axle RPM triggers protection before a smoothed RPM reading catches up");
  for(int i=0;i<30;i++)surgeGuard.Step(.01,true,1,Wheel(surgeGuard,6600));
  Check(surgeGuard.AvailableTorqueNm==0,"race firing pulses cannot keep powering an axle above the torque ceiling");
  var neutral=new EngineCore{GearRatio=0,RaceLimiter=true};for(int i=0;i<600;i++)neutral.Step(.01,true,1,0);Check(neutral.LimiterActive&&neutral.Boost==0,"race limiter can bounce in neutral without loaded turbo boost");
 }
}
