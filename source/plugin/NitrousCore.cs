using System;
namespace KerbalDriftAgency {
 // Arcade bottle units: seconds of spray at a 100 hp shot. Never uses warp time.
 public sealed class NitrousCore {
  public double Charge=1;public bool Active,DriftRecharge;double driftTime;
  public static double Capacity(int bottle){return bottle<=0?6:bottle==1?12:24;}
  static double Safe(double n,double fallback){return double.IsNaN(n)||double.IsInfinity(n)?fallback:n;}
  public double Step(double dt,bool enabled,int bottle,double shot,bool held,bool eligible,bool drifting,double rpm){
   Active=DriftRecharge=false;dt=Math.Max(0,Math.Min(.1,Safe(dt,0)));Charge=Math.Max(0,Math.Min(1,Safe(Charge,1)));
   if(dt==0||!enabled){driftTime=0;return 0;}
   shot=Math.Max(25,Math.Min(300,Safe(shot,100)));double capacity=Capacity(bottle);
   driftTime=drifting?Math.Min(1,driftTime+dt):0;
   if(held){
    if(!eligible||Charge<=0)return 0;
    double needed=dt*shot/100,used=Math.Min(Charge*capacity,needed);Charge=Math.Max(0,Charge-used/capacity);Active=used>0;
    return used/needed*shot*745.699872/Math.Max(1500,Safe(rpm,1500))*60/(2*Math.PI);
   }
   DriftRecharge=driftTime>=.5&&Charge<1;Charge=Math.Min(1,Charge+dt*.2*(DriftRecharge?3:1)/capacity);return 0;
  }
 }
}
