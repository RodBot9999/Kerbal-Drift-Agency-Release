using System;

namespace KerbalDriftAgency {
 public sealed class BoostReleaseTrigger {
  double previousInput;bool previousShift;
  public bool Step(double input,bool shifting,bool turbo,double pressure){
   bool release=(previousInput>.15&&input<=.15)||(shifting&&!previousShift);
   previousInput=input;previousShift=shifting;
   return turbo&&pressure>.08&&release;
  }
 }
 // Power/energy estimate for stock LiquidFuel (5 kg per unit). Fuel is
 // gasoline-equivalent until a separately authorized source-data import exists.
 public static class FuelConsumptionCore {
  static double Clamp(double value,double lo,double hi){return Math.Max(lo,Math.Min(hi,value));}
  static double Finite(double value,double fallback){return double.IsNaN(value)||double.IsInfinity(value)?fallback:value;}
  public static double Multiplier(double value){return Clamp(Finite(value,1),.1,3);}
  public static double UnitsPerSecond(bool running,bool cut,double rpm,double torqueNm,double throttle,double load,bool neutral,double multiplier){
   if(!running||cut)return 0;
   rpm=Math.Max(0,Finite(rpm,0));torqueNm=Math.Max(0,Finite(torqueNm,0));
   throttle=Clamp(Finite(throttle,0),0,1);
   // Loaded output scales with current native load; free revving only needs
   // internal/accessory work rather than the engine's full rated shaft power.
   double workLoad=neutral?.03:Clamp(Finite(load,1),.03,1);
   double watts=torqueNm*rpm*Math.PI/30*workLoad;
   double thermalEfficiency=.18+.12*Math.Sqrt(throttle);
   double idleKgPerSecond=.8/3600*Clamp(1+.5*(rpm/900-1),1,4);
   double kgPerSecond=idleKgPerSecond+watts/(46400000*thermalEfficiency);
   return kgPerSecond/5*Multiplier(multiplier);
  }
 }
 // Independent of Unity so engine behaviour can be exercised outside KSP.
 public sealed class EngineCore {
  public static double ConfiguredIdle(double rpm,bool imported){return Clamp(double.IsNaN(rpm)||double.IsInfinity(rpm)?900:rpm,500,imported?5000:1500);}
  public double IdleRpm=900, LimitRpm=6500, LaunchRpm=2400;
  public double GearRatio=.95, FinalDrive=3.89, Efficiency=.90;
  public double MaxBoost=.8, BoostStartRpm=1800, BoostFullRpm=3600;
  public double SpoolSeconds=1.1, ReleaseSeconds=.28, BoostTorqueGain=.75;
  public double RpmResponseSeconds=.22, IdleFuelRate=.0015, FullFuelRate=.024;
  public double Rpm, Boost, Throttle, AvailableTorqueNm, OutputTorqueNm, FuelRate;
  public double FuelConsumptionMultiplier=1;
  public double TransmissionScale=1;
  public bool Shifting;
  public double ShiftBoostRetention=.40, ShiftReleaseSeconds=.12;
  public double SpoolExponent=1, ParasiticFactor=1;
  public double TorqueRpmScale=1;
  // Rotor energy and manifold pressure have different release times.
  public bool MechanicalBoost, RaceLimiter;
  public double EngineLoad=1, Rotor, RotorDecaySeconds=1.15;
  public double FlowTaperStartRpm=9000, FlowTaperEndRpm=10000, HighRpmBoostFraction=1;
  public double ThresholdShape=1;
  public bool ImportedBoostMap;
  public bool TwinCharged;
  public double MechanicalMaxBoost, MechanicalEngageRpm, MechanicalDisengageRpm;
  public double ImportedPeakBoost;
  public double[] BoostRpm={},BoostGauge={};
  public double EvaluateBoost(double rpm){
   if(BoostRpm.Length==0)return 0;
   if(rpm<=BoostRpm[0])return BoostGauge[0];
   for(int i=1;i<BoostRpm.Length;i++)if(rpm<=BoostRpm[i])return BoostGauge[i-1]+(BoostGauge[i]-BoostGauge[i-1])*(rpm-BoostRpm[i-1])/(BoostRpm[i]-BoostRpm[i-1]);
   return BoostGauge[BoostGauge.Length-1];
  }
  public bool LimiterActive;
  double limiterPhase;
  public bool Limiter;
  public double[] CurveRpm={900,1500,2500,3500,4500,5500,6500};
  public double[] CurveTorque={110,150,180,190,185,170,135};
  public static double Clamp(double x,double lo,double hi) {return Math.Max(lo,Math.Min(hi,x));}
  static double Approach(double value,double target,double dt,double seconds) {
   return value+(target-value)*(1-Math.Exp(-dt/Math.Max(.01,seconds)));
  }
  public double EvaluateTorque(double rpm) {
   if(rpm<=CurveRpm[0])return CurveTorque[0];
   for(int i=1;i<CurveRpm.Length;i++)if(rpm<=CurveRpm[i]) {
    double f=(rpm-CurveRpm[i-1])/(CurveRpm[i]-CurveRpm[i-1]);
    return CurveTorque[i-1]+(CurveTorque[i]-CurveTorque[i-1])*f;
   }
   return CurveTorque[CurveTorque.Length-1];
  }
  public void Step(double dt,bool running,double throttle,double drivenWheelRpm) {
   dt=Clamp(dt,0,.1); throttle=Clamp(Math.Abs(throttle),0,1);
   Throttle=Approach(Throttle,running?throttle:0,dt,.12);
   double coupledRpm=Math.Abs(drivenWheelRpm)*GearRatio*FinalDrive;
   // Simple launch clutch: permit revs at rest, progressively couple to the axle.
   double launchFloor=IdleRpm+(LaunchRpm-IdleRpm)*Throttle;
   double target=running?(GearRatio==0?IdleRpm+(LimitRpm-IdleRpm+(RaceLimiter?60:-100))*Throttle:Math.Max(launchFloor,coupledRpm)):0;
   if(RaceLimiter&&Limiter&&target<LimitRpm+300)target=Math.Min(target,LimitRpm-100);
   Rpm=Approach(Rpm,target,dt,running?RpmResponseSeconds:.35);
   double protectionRpm=RaceLimiter?Math.Max(Rpm,coupledRpm):Rpm;
   if(protectionRpm>=LimitRpm&&running){if(!LimiterActive)limiterPhase=0;LimiterActive=true;}
   else if(protectionRpm<LimitRpm-(RaceLimiter?80:250) || !running)LimiterActive=false;
   Limiter=LimiterActive&&(!RaceLimiter||limiterPhase<.04||protectionRpm>LimitRpm+300);
   if(LimiterActive)limiterPhase=(limiterPhase+dt)%.12;else limiterPhase=0;
   double spool=Clamp((Rpm-BoostStartRpm)/Math.Max(100,BoostFullRpm-BoostStartRpm),0,1);
   spool=Math.Pow(spool*spool*(3-2*spool),Clamp(SpoolExponent*ThresholdShape,1,3));
   double pedal=Clamp((Throttle-.15)/.85,0,1);
   double load=GearRatio==0?0:Clamp(EngineLoad,0,1)*pedal;
   double exhaust=running?load*(Shifting?.20:1)*(Limiter?(RaceLimiter?.90: .25):1):0;
   // The rotor can store energy before manifold boost crosses its threshold.
   double rotorTarget=Clamp((Rpm-IdleRpm)/Math.Max(100,BoostFullRpm-IdleRpm),0,1)*exhaust;
   Rotor=Approach(Rotor,MechanicalBoost?0:rotorTarget,dt,rotorTarget>Rotor?SpoolSeconds:RotorDecaySeconds);
   double taper=1-(1-Clamp(HighRpmBoostFraction,0,1))*Clamp((Rpm-FlowTaperStartRpm)/Math.Max(100,FlowTaperEndRpm-FlowTaperStartRpm),0,1);
   double boostTarget=running?MaxBoost*taper*spool*(MechanicalBoost?pedal:Rotor*pedal):0;
   if(ImportedBoostMap&&BoostRpm.Length>1)boostTarget=running?EvaluateBoost(Rpm*TorqueRpmScale)*MaxBoost/Math.Max(.001,ImportedPeakBoost)*(MechanicalBoost?pedal:Rotor*pedal):0;
   if(ImportedBoostMap&&TwinCharged&&BoostRpm.Length>1){
    // One exported combined steady-state pressure target. The mechanical
    // stage fills its capped share immediately; the remainder follows rotor
    // energy. Never multiply two boost gains into an already boosted curve.
    double sourceRpm=Rpm*TorqueRpmScale;
    double scale=MaxBoost/Math.Max(.001,ImportedPeakBoost);
    double combined=EvaluateBoost(sourceRpm)*scale;
    double engage=MechanicalEngageRpm<=0?1:Clamp((sourceRpm-MechanicalEngageRpm)/Math.Max(100,MechanicalEngageRpm*.1),0,1);
    double disengage=MechanicalDisengageRpm<=0?1:Clamp((MechanicalDisengageRpm-sourceRpm)/Math.Max(100,MechanicalDisengageRpm*.1),0,1);
    double mechanical=Math.Min(combined,Math.Max(0,MechanicalMaxBoost)*scale)*engage*disengage;
    boostTarget=running?(mechanical+(combined-mechanical)*Rotor)*pedal:0;
   }
   if(Shifting)boostTarget*=Clamp(ShiftBoostRetention,0,1);
   // A short race ignition cut is not a throttle lift or an anti-lag system.
   if(Limiter)boostTarget*=RaceLimiter?.95:0;
   Boost=Approach(Boost,boostTarget,dt,boostTarget>Boost?.08:Shifting?ShiftReleaseSeconds:ReleaseSeconds);
   AvailableTorqueNm=running&&!Limiter?EvaluateTorque(Rpm*TorqueRpmScale)*(1+Boost*BoostTorqueGain)*Clamp(ParasiticFactor,.5,1):0;
   // Reduced firing torque near the ceiling prevents repeated pulses from
   // continuing to accelerate the actual axle while the RPM display catches up.
   if(RaceLimiter&&LimiterActive)AvailableTorqueNm*=Clamp((LimitRpm+80-protectionRpm)/100,0,1);
   OutputTorqueNm=AvailableTorqueNm*Throttle;
   // Meter fuel only: this has no effect on engine drag, torque or steering.
   bool coastFuelCut=GearRatio!=0&&!Shifting&&throttle<.01&&Throttle<.02&&coupledRpm>IdleRpm*1.5;
   FuelRate=FuelConsumptionCore.UnitsPerSecond(running,Limiter||coastFuelCut,Rpm,OutputTorqueNm,Throttle,EngineLoad,GearRatio==0,FuelConsumptionMultiplier);
  }
  public double WheelTorqueKnM(int drivenWheels) {
   return drivenWheels>0?AvailableTorqueNm*GearRatio*FinalDrive*Efficiency*TransmissionScale/(1000*drivenWheels):0;
  }
 }
}
