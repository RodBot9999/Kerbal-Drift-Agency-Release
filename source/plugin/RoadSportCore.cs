using System;

// Road/Sport profiles use the tested Race recovery law. Race and Drift
// themselves continue through their existing paths and state objects.
public static class RoadSportCore {
 public static bool Applies(int mode){return mode==0||mode==1;}
 static double Clamp(double x,double lo,double hi){return Math.Max(lo,Math.Min(hi,x));}
 public static double Tolerance(int mode,double configured){return Clamp(configured*(mode==0?.30:.60),0,mode==0?3:6);}
 public static double RecoveryLock(double maxAngle,double assistance){return KDACore.RecoveryLock(2,maxAngle,assistance);}
 public static double SteeringTau(int mode,double current,double target,double configured){return KDACore.SteeringTau(current,target,Math.Min(configured,mode==0?.14:.12),mode);}
 public static KDACore.Result Evaluate(int mode,double strength,double allowed,double speed,double steer,double maxAngle,double wheelbase,double gravity,double slip,double yaw,double roll,double unloading,bool grounded,bool intentional,double capacity,double slipRate=0,double currentYaw=Double.NaN,double yawRate=0,double settling=0){
  // Scale the Race budget to the existing Road/Sport g budgets; its recovery
  // prediction and correcting lock remain fast and independent of turn-in.
  return KDACore.Evaluate(2,strength,allowed,speed,steer,maxAngle,wheelbase,gravity,slip,yaw,roll,unloading,grounded,intentional,capacity*(mode==0?.48:.70)/.95,50,slipRate,currentYaw,yawRate,settling);
 }
 public static double ThrottleRise(int mode,bool enabled,double configured){return enabled&&Applies(mode)?Math.Max(configured,mode==0?.55:.35):configured;}
 public static double PowerCap(int mode,bool enabled,bool tcs){return mode==0&&enabled&&tcs?.60:1;}
 public static double PedalInput(double input,double pedal,int mode,bool enabled){return enabled&&Applies(mode)?Math.Sign(input)*Math.Min(Math.Abs(input),Clamp(Math.Abs(pedal),0,1)):input;}
 public static double TractionTarget(double slip,bool grounded,int mode){
  if(!Applies(mode))return DriverAidsCore.Traction(slip,grounded,mode);
  if(!grounded)return mode==0?.03:.50;
  return Clamp(1-Math.Max(0,slip-(mode==0?.025:.12))*(mode==0?10:2.5),mode==0?.03:.50,1);
 }
 public static double TractionBrake(double road,double tread,bool grounded,int mode){
  if(mode!=0)return DriverAidsCore.TractionBrake(road,tread,grounded,mode);
  if(!grounded)return 0;
  double excess=Math.Abs(tread)-Math.Abs(road)-Math.Max(.15,Math.Abs(road)*.02);
  return Clamp(excess*.06,0,.18);
 }
}

public sealed class RoadSportTraction {
 public double Scale=1;
 public void Reset(){Scale=1;}
 public double Step(double dt,bool enabled,double slip,bool grounded,int mode){
  if(!enabled||!RoadSportCore.Applies(mode)){Reset();return Scale;}
  dt=Math.Max(0,Math.Min(.1,dt));
  double target=RoadSportCore.TractionTarget(slip,grounded,mode);
  double rate=target<Scale?(mode==0?3:1.25):(mode==0?.45:.75);
  Scale+=Math.Max(-dt*rate,Math.Min(dt*rate,target-Scale));return Scale;
 }
}
