using System;
namespace KerbalDriftAgency {
 // An independent AWD driver trim layered after the established RWD controller.
 // The driver can progressively reach neutral/slightly into-slide steering;
 // releasing the key hands the balancing angle back without stored windup.
 public sealed class AwdDriftTrim {
  public double Authority {get;private set;}
  public double AddedSteering {get;private set;}
  public double RotationIntent {get;private set;}
  static double Clamp(double v,double lo,double hi){return Math.Max(lo,Math.Min(hi,v));}
  public void Reset(){Authority=AddedSteering=RotationIntent=0;}
  public double Step(double dt,bool enabled,bool drifting,double input,double correction,double baseline,double slip,double speed,double physicalLock,double scLock){
   if(!enabled||!drifting||speed<4||physicalLock<=0){Reset();return baseline;}
   dt=Clamp(dt,0,.1);input=Clamp(input,-1,1);
   double support=Clamp((Math.Abs(slip)-8)/8,0,1)*Clamp((85-Math.Abs(slip))/15,0,1);
   bool reducing=input*correction<-.002&&input*slip>0&&support>0;
   double target=reducing?input:0,step=dt/(reducing?.60:.32);
   Authority+=Clamp(target-Authority,-step,step);
   bool aligned=Authority*correction<0&&Authority*slip>0;
   RotationIntent=aligned?Authority*support:0;
   double sign=Math.Sign(RotationIntent),margin=Math.Min(6,scLock*.12)/physicalLock;
   double extra=sign*Math.Abs(RotationIntent)*Math.Max(0,margin-sign*baseline);
   AddedSteering+=(extra-AddedSteering)*(1-Math.Exp(-dt/.07));
   double total=Math.Min(1,(scLock+15)/physicalLock);
   return Clamp(baseline+AddedSteering,-total,total);
  }
 }
 public sealed class AwdHandbrakeClutch {
  double engagement=1;
  public double Coupling {get;private set;}
  public AwdHandbrakeClutch(){Reset();}
  public void Reset(){engagement=Coupling=1;}
  public void Step(double dt,bool supported,bool handbrake){
   if(!supported){Reset();return;}
   engagement=handbrake?0:Math.Min(1,engagement+Math.Max(0,Math.Min(.1,dt))/.28);
   Coupling=engagement*engagement*(3-2*engagement);
  }
  public static double CoupledMean(double frontWeight,double frontRpm,double rearWeight,double rearRpm,double coupling){
   if(frontWeight<=0||rearWeight<=0)coupling=1;
   double sum=frontWeight+rearWeight*coupling;
   return sum>0?(frontWeight*frontRpm+rearWeight*coupling*rearRpm)/sum:0;
  }
  public static ActiveDriveAllocation Gate(double fl,double fr,double rl,double rr,double coupling){
   double front=fl+fr,rear=rl+rr;
   if(front<=0||rear<=0)coupling=1;coupling=Math.Max(0,Math.Min(1,coupling));
   double scale=front>0?(front+rear*(1-coupling))/front:1;
   return new ActiveDriveAllocation{FL=fl*scale,FR=fr*scale,RL=rl*coupling,RR=rr*coupling};
  }
 }
 public static class AwdDriftDemand {
  public static double RearFloor(double baseline,double slip,double intent){
   double slide=Math.Max(0,Math.Min(1,(Math.Abs(slip)-8)/12));
   return Math.Min(.85,baseline+.15*slide+.10*Math.Abs(intent));
  }
  // Only the driver's sustained rotation request creates extra yaw demand.
  // Neutral keys do not ask the electronic axle to straighten a natural drift.
  public static double YawTarget(double currentYaw,double intent,double limit){
   if(Math.Abs(intent)<.001)return currentYaw;
   return Math.Max(-limit,Math.Min(limit,currentYaw-.20*intent));
  }
 }
}
