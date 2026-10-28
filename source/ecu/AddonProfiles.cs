// SPDX-License-Identifier: MIT
using System;
namespace KerbalDriftAgency.ECU {
 public static class AddonProfile {
  public const double NormalSlipDegrees=5;
  public static int Normalize(int profile){return Math.Max(0,Math.Min(1,profile));}
  public static int Mode(int profile){return Normalize(profile)==1?2:0;}
  public static double Tolerance(int profile){return Normalize(profile)==1?NormalSlipDegrees:4;}
  public static double YawBrake(int profile,double error,double side,bool front){return DriverAidsCore.YawBrake(error,side,front,Mode(profile));}
  public static double TractionBrake(int profile,double road,double tread,bool grounded){return DriverAidsCore.TractionBrake(road,tread,grounded,Normalize(profile)==0?0:1);}
 }
 // Brake-pressure relief only. No forces, attitude commands or power changes.
 public sealed class AddonPitchGuard {
  public double Scale=1;public bool Triggered {get;private set;}double hold;
  public void Reset(){Scale=1;hold=0;Triggered=false;}
  public double Step(double dt,int profile,double brake,double speed,double noseDownDegrees,double noseDownRate,double rearFraction,double baselineRearFraction,bool rearFullyUnloaded,bool frontContact){
   profile=AddonProfile.Normalize(profile);dt=EcuCore.Clean(dt,0,0,.1);brake=EcuCore.Clean(brake,0,0,1);
   if(brake<.02||speed<2||!frontContact||double.IsNaN(noseDownDegrees+noseDownRate+rearFraction)){Reset();return Scale;}
   double relative=EcuCore.Clean(rearFraction,1,0,1)/Math.Max(.05,EcuCore.Clean(baselineRearFraction,.5,.05,.95));
   bool diving=noseDownDegrees>3||noseDownRate>.08;
   // Normal has no predictive load threshold: actual zero-load/lost-contact
   // evidence is mandatory before the first intervention.
   bool danger=profile==1?rearFullyUnloaded&&diving:(relative<.65&&(noseDownDegrees>1.5||noseDownRate>.04))||(noseDownDegrees>2&&noseDownRate>.18)||rearFullyUnloaded&&diving;
   Triggered=danger;if(danger)hold=profile==1?.12:.20;else hold=Math.Max(0,hold-dt);
   double target=1;
   if(hold>0){
    double severity=Math.Max(EcuCore.Clean((noseDownDegrees-1.5)/8,0,0,1),EcuCore.Clean(noseDownRate/.7,0,0,1));
    target=profile==1?.72-.12*severity:Math.Max(.12,.60-.48*Math.Max(severity,1-relative));
   }
   Scale+=(target-Scale)*(1-Math.Exp(-dt/(target<Scale?.025:.40)));
   return Scale;
  }
 }
 public sealed class AddonSteering {
  readonly StabilityObserver observer=new StabilityObserver();readonly RaceRecoveryBlend race=new RaceRecoveryBlend();
  double driver,output;public double YawError,Limit=1,Correction;
  public void Reset(){driver=output=YawError=Correction=0;Limit=1;observer.Reset();race.Reset();}
  public double Step(double dt,int profile,bool adaptive,bool yawAid,double strength,double raw,double speed,double angle,double wheelbase,double gravity,double slip,double yaw,double roll,double unloading,double budget,bool grounded){
   profile=AddonProfile.Normalize(profile);dt=EcuCore.Clean(dt,0,0,.1);raw=EcuCore.Clean(raw,0,-1,1);strength=EcuCore.Clean(strength,1,0,1);int mode=AddonProfile.Mode(profile);double tolerance=AddonProfile.Tolerance(profile);
   if(!grounded||speed<4||strength<=0||!adaptive&&!yawAid||double.IsNaN(speed+slip+yaw+angle)||double.IsInfinity(speed+slip+yaw+angle)){Reset();driver=output=raw;return raw;}
   double capacity=EcuCore.Clean(budget,.65,.05,2)/(mode>=2?.95:.48);
   double sample=profile==1&&Math.Abs(slip)<=AddonProfile.NormalSlipDegrees?0:slip;
   var r=KDACore.Evaluate(mode,strength,tolerance,speed,raw,angle,wheelbase,gravity,sample,yaw,roll,unloading,true,false,capacity,50);
   double safe=Math.Atan(EcuCore.Clean(budget,.65,.05,2)*Math.Max(.1,gravity)*Math.Max(.2,wheelbase)/(speed*speed))/Math.Max(.001,angle*Math.PI/180);
   Limit=adaptive?Math.Min(r.Limit,1-strength*(1-Math.Min(1,safe))):1;
   double demand=Math.Max(-Limit,Math.Min(Limit,raw));driver+=(demand-driver)*(1-Math.Exp(-dt/KDACore.SteeringTau(driver,demand,.16,mode)));
   double desired=-(driver*Math.Tan(angle*Math.PI/180))*speed/Math.Max(.2,wheelbase);
   observer.Step(dt,slip,yaw,desired,tolerance);
   double observed=profile==1&&Math.Abs(slip)<=AddonProfile.NormalSlipDegrees?0:observer.Slip;
   r=KDACore.Evaluate(mode,strength,tolerance,speed,driver,angle,wheelbase,gravity,observed,observer.Yaw,roll,unloading,true,false,capacity,50,observed==0?0:observer.SlipRate,yaw,observer.YawRate);
   Correction=yawAid&&observer.Active?r.Correction:0;YawError=yawAid?DriverAidsCore.ExcessYaw(observer.Yaw,desired):0;
   double cap=yawAid&&observer.Active?Math.Max(Limit,r.RecoveryLimit):Limit,ordinary=Math.Max(-cap,Math.Min(cap,driver+Correction));
   if(profile==1&&yawAid)output=race.Step(dt,observer.Active,raw,Correction,driver,output,angle,cap,slip,yaw,desired);
   else{race.Reset();output=ordinary;}
   return Math.Max(-1,Math.Min(1,output));
  }
 }
}
