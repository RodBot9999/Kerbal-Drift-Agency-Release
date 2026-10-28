using System;
using KerbalDriftAgency;
class EngineCoreTests {
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static void Run(EngineCore e,double seconds,bool on,double throttle,double rpm,double dt=.02){for(int i=0;i<(int)(seconds/dt);i++)e.Step(dt,on,throttle,rpm);}
 static void Main(){
  var e=new EngineCore();Run(e,3,true,0,0);
  Check(Math.Abs(e.Rpm-900)<1 && e.Boost<.001 && e.OutputTorqueNm<.001,"idle without boost or drive torque");
  Run(e,.2,true,1,900);double early=e.Boost;Run(e,4,true,1,900);
  Check(e.Boost>early+.3 && e.Boost<=.8,"boost builds gradually under load");
  Check(e.OutputTorqueNm>e.EvaluateTorque(e.Rpm)*1.3,"boost increases available torque");
  double fueled=e.FuelRate;Run(e,2,true,0,900);
  Check(e.Boost<.01 && e.FuelRate<fueled,"lift-off releases boost and reduces fuel use");
  Check(Math.Abs(e.WheelTorqueKnM(2)*2-e.WheelTorqueKnM(4)*4)<1e-9,"total axle torque independent of driven wheel count");
  Run(e,3,true,1,2300);Check(e.Limiter && e.OutputTorqueNm==0,"rev limiter cuts torque on overspeed");
  Run(e,3,true,1,1200);Check(!e.Limiter && e.OutputTorqueNm>0,"limiter recovers below hysteresis threshold");
  Run(e,3,false,1,1200);Check(e.OutputTorqueNm==0 && e.FuelRate==0 && e.Boost<.001,"engine off removes torque and fuel consumption");
  var a=new EngineCore();var b=new EngineCore();Run(a,4,true,1,900,.02);Run(b,4,true,1,900,.01);
  Check(Math.Abs(a.Boost-b.Boost)<.01 && Math.Abs(a.Rpm-b.Rpm)<1,"consistent response across physics step sizes");
  var reverse=new EngineCore();var forward=new EngineCore();Run(reverse,3,true,-1,-900);Run(forward,3,true,1,900);
  Check(Math.Abs(reverse.OutputTorqueNm-forward.OutputTorqueNm)<1e-9,"reverse request produces equal torque magnitude");
  Check(e.WheelTorqueKnM(0)==0,"no driven wheels yields no wheel torque");
  var neutral=new EngineCore();neutral.GearRatio=0;Run(neutral,4,true,1,900);
  Check(neutral.Rpm>6000 && neutral.WheelTorqueKnM(2)==0 && neutral.Boost==0,"neutral permits revs without wheel torque or loaded boost");
  var shift=new EngineCore();Run(shift,8,true,1,1200);double beforeShift=shift.Boost;
  shift.Shifting=true;shift.TransmissionScale=0;Run(shift,.3,true,1,1200);
  Check(shift.Boost<beforeShift*.55 && shift.Boost>beforeShift*.35 && shift.WheelTorqueKnM(2)==0,"shift releases boost partially while wheel torque is cut");
  double afterShift=shift.Boost;shift.Shifting=false;shift.TransmissionScale=1;Run(shift,.1,true,1,1200);
  Check(shift.Boost>afterShift && shift.Boost<beforeShift*.95,"retained rotor energy rebuilds pressure after shift without an instant reset");
  Run(shift,5,true,1,1200);Check(shift.Boost>beforeShift*.98,"boost returns after shift");
  var smooth=new EngineCore();var kick=new EngineCore();kick.SpoolExponent=3;
  Run(smooth,8,true,1,750);Run(kick,8,true,1,750);
  Check(kick.Boost<smooth.Boost*.6,"boost kick delays the surge at intermediate RPM");
  Run(smooth,8,true,1,1200);Run(kick,8,true,1,1200);
  Check(Math.Abs(smooth.Boost-kick.Boost)<.005,"boost kick retains the full-pressure ceiling");
  var sc=new EngineCore();sc.ParasiticFactor=.9;Run(sc,5,true,1,1200);
  Check(Math.Abs(sc.AvailableTorqueNm/(sc.EvaluateTorque(sc.Rpm)*(1+sc.Boost*sc.BoostTorqueGain))-.9)<1e-6,"supercharger applies crankshaft power cost");
  var releases=new BoostReleaseTrigger();Run(shift,5,true,1,1200);releases.Step(1,false,true,shift.Boost);
  Run(shift,.08,true,0,1200);bool first=releases.Step(0,false,true,shift.Boost);
  Run(shift,.08,true,1,1200);releases.Step(1,false,true,shift.Boost);
  Run(shift,.08,true,0,1200);bool second=releases.Step(0,false,true,shift.Boost);
  Check(first&&second&&shift.Boost>.08,"two rapid throttle lifts both trigger BOV with residual pressure");
  Check(!releases.Step(0,false,true,shift.Boost),"holding released throttle does not repeat BOV");
  Check(releases.Step(1,true,true,shift.Boost)&&!releases.Step(1,true,true,shift.Boost),"boosted shift triggers BOV once");
  releases.Step(1,false,true,0);Check(!releases.Step(0,false,true,.04),"no BOV without sufficient pressure");
  releases.Step(1,false,false,.8);Check(!releases.Step(0,false,false,.8),"supercharger and unboosted setups suppress BOV");
 }
}
