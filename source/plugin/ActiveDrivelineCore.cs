using System;
namespace KerbalDriftAgency {
 // Independent, bounded torque allocation for explicitly imported hardware.
 // No extra forces, brakes or power source: the four fractions always sum to 1.
 public struct DrivelineWheelState {
  public double Weight,Slip,ShaftSpeed,ExpectedShaftSpeed;
  public bool Grounded;
 }
 public struct ActiveDrivelineConfig {
  public bool Center,FrontVectoring,RearVectoring;
  public int PassiveMode;
  public double CenterOffset,CenterRate,SpeedThreshold,SpeedDifferenceThreshold;
  public double FrontOffset,RearOffset,FrontRate,RearRate;
  public double DriftRearMinimum,PoweredRearMinimum;
 }
 public struct ActiveDriveAllocation {
  public double FL,FR,RL,RR,RearShare,FrontLeftShare,RearLeftShare,YawError;
 }
 public sealed class ActiveDrivelineCore {
  bool initialized;double rear,frontLeft,rearLeft;
  static bool Finite(double x){return !Double.IsNaN(x)&&!Double.IsInfinity(x);}
  static double Clamp(double x,double lo,double hi){return Finite(x)?Math.Max(lo,Math.Min(hi,x)):lo;}
  static double Move(double value,double target,double rate,double dt){return value+Clamp(target-value,-Math.Max(0,rate)*dt,Math.Max(0,rate)*dt);}
  static bool Valid(DrivelineWheelState s){return Finite(s.Weight)&&s.Weight>=0&&Finite(s.Slip)&&s.Slip>=0&&Finite(s.ShaftSpeed)&&s.ShaftSpeed>=0&&Finite(s.ExpectedShaftSpeed)&&s.ExpectedShaftSpeed>=0;}
  static double Contact(DrivelineWheelState a,DrivelineWheelState b){return ((a.Grounded?1:0)+(b.Grounded?1:0))*.5;}
  static double Excess(DrivelineWheelState a,DrivelineWheelState b){return (Math.Max(0,a.ShaftSpeed-a.ExpectedShaftSpeed)+Math.Max(0,b.ShaftSpeed-b.ExpectedShaftSpeed))*.5;}
  static double VectorTarget(DrivelineWheelState left,DrivelineWheelState right,double baseline,double offset,double yawError,bool yawAllowed){
   if(!left.Grounded&&!right.Grounded)return baseline;
   double traction=.25*Clamp(right.Slip-left.Slip,-2,2);
   double contact=.5*((left.Grounded?1:0)-(right.Grounded?1:0));
   // Positive yaw about body-up is a right turn. More left-wheel drive gives
   // that yaw moment; no steering or body rotation is injected here.
   double yaw=yawAllowed&&left.Grounded&&right.Grounded?.25*yawError*(1-Clamp((left.Slip+right.Slip)*.5,0,1)):0;
   return Clamp(baseline+traction+contact+yaw,Math.Max(.05,baseline-offset),Math.Min(.95,baseline+offset));
  }
  public void Reset(){initialized=false;}
  public ActiveDriveAllocation Step(double dt,ActiveDrivelineConfig c,DrivelineWheelState fl,DrivelineWheelState fr,DrivelineWheelState rl,DrivelineWheelState rr,double desiredYaw,double actualYaw,bool powered,bool yawAllowed){
   // Config is source calibration. Sanitizing remains necessary for edited CFGs.
   bool valid=Valid(fl)&&Valid(fr)&&Valid(rl)&&Valid(rr)&&Finite(desiredYaw)&&Finite(actualYaw)&&Finite(dt)&&dt>=0;
   if(!valid){
    Reset();double a=Finite(fl.Weight)&&fl.Weight>0?fl.Weight:0,b=Finite(fr.Weight)&&fr.Weight>0?fr.Weight:0,d=Finite(rl.Weight)&&rl.Weight>0?rl.Weight:0,e=Finite(rr.Weight)&&rr.Weight>0?rr.Weight:0,sum=a+b+d+e;
    return sum>0?new ActiveDriveAllocation{FL=a/sum,FR=b/sum,RL=d/sum,RR=e/sum,RearShare=(d+e)/sum,FrontLeftShare=a+b>0?a/(a+b):.5,RearLeftShare=d+e>0?d/(d+e):.5}:new ActiveDriveAllocation();
   }
   dt=Clamp(dt,0,.1);
   double f=fl.Weight+fr.Weight,r=rl.Weight+rr.Weight,total=f+r;
   if(total<=0){Reset();return new ActiveDriveAllocation();}
   double baseRear=r/total,baseFL=f>0?fl.Weight/f:.5,baseRL=r>0?rl.Weight/r:.5;
   if(!initialized){rear=baseRear;frontLeft=baseFL;rearLeft=baseRL;initialized=true;}
   double targetRear=baseRear;
   if(c.Center&&f>0&&r>0&&powered&&(Contact(fl,fr)+Contact(rl,rr)>0)){
    double fc=Contact(fl,fr),rc=Contact(rl,rr),fs=(fl.ShaftSpeed+fr.ShaftSpeed)*.5,rs=(rl.ShaftSpeed+rr.ShaftSpeed)*.5;
    double fe=Excess(fl,fr),re=Excess(rl,rr),offset=Clamp(c.CenterOffset,0,.95);
    double shift=0;
    // Expected road speeds remove normal cornering, tire-size and final-drive
    // differences. Only excess shaft speed or loss of contact transfers drive.
    if(fs+rs>Clamp(c.SpeedThreshold,0,100000)&&Math.Abs(fe-re)>Clamp(c.SpeedDifferenceThreshold,0,100000))
     shift=.5*(fe-re)/Math.Max(10,(fs+rs)*.5);
    shift+=.5*(rc-fc);
    targetRear=Clamp(baseRear+shift,Math.Max(.05,baseRear-offset),Math.Min(.95,baseRear+offset));
    if(Finite(c.DriftRearMinimum)&&c.DriftRearMinimum>0)
     targetRear=Math.Max(targetRear,Clamp(c.DriftRearMinimum,Math.Max(.05,baseRear-offset),Math.Min(.95,baseRear+offset)));
   }
   // An AWD cornering request may relieve front-wheel drive saturation, but
   // cannot create hardware, exceed source bias bounds or act while coasting.
   if(c.Center&&f>0&&r>0&&powered&&Finite(c.PoweredRearMinimum)&&c.PoweredRearMinimum>0&&Contact(fl,fr)==1&&Contact(rl,rr)==1)
    targetRear=Math.Max(targetRear,Clamp(c.PoweredRearMinimum,Math.Max(.05,baseRear-Clamp(c.CenterOffset,0,.95)),Math.Min(.95,baseRear+Clamp(c.CenterOffset,0,.95))));
   rear=c.Center&&f>0&&r>0?Move(rear,targetRear,Clamp(c.CenterRate,0,20),dt):baseRear;
   double yawError=Clamp(desiredYaw-actualYaw,-1,1);
   double ft=DrivelineCore.LeftShare(fl.Weight,fr.Weight,fl.Slip,fr.Slip,c.PassiveMode);
   double rt=DrivelineCore.LeftShare(rl.Weight,rr.Weight,rl.Slip,rr.Slip,c.PassiveMode);
   if(c.FrontVectoring)ft=powered?VectorTarget(fl,fr,baseFL,Clamp(c.FrontOffset,0,.95),yawError,yawAllowed):baseFL;
   if(c.RearVectoring)rt=powered?VectorTarget(rl,rr,baseRL,Clamp(c.RearOffset,0,.95),yawError,yawAllowed):baseRL;
   frontLeft=Move(frontLeft,ft,c.FrontVectoring?Clamp(c.FrontRate,0,20):c.PassiveMode==3?12.5:5,dt);
   rearLeft=Move(rearLeft,rt,c.RearVectoring?Clamp(c.RearRate,0,20):c.PassiveMode==3?12.5:5,dt);
   if(f<=0)rear=1;if(r<=0)rear=0;
   // Complementary assignments conserve the engine allocation exactly, before
   // the existing per-wheel TCS can reduce its own wheel torque.
   double frontShare=1-rear;
   return new ActiveDriveAllocation{FL=frontShare*frontLeft,FR=frontShare*(1-frontLeft),RL=rear*rearLeft,RR=rear*(1-rearLeft),RearShare=rear,FrontLeftShare=frontLeft,RearLeftShare=rearLeft,YawError=yawAllowed?yawError:0};
  }
 }
}
