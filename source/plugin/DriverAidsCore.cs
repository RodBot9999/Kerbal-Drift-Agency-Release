using System;
public static class DriverAidsCore {
 public static double ServiceBrake(double pressure,int mode,bool front){return mode==3&&!front?0:Math.Max(0,Math.Min(1,pressure));}
 public static double Traction(double slip,bool grounded){return grounded?Math.Max(.02,Math.Min(1,1-Math.Max(0,slip-.06)*3.5)):0;}
 public static double Traction(double slip,bool grounded,int mode){
  if(mode==3)return 1;
  double floor=mode==2?.70:mode==1?.45:.35;
  if(!grounded)return floor;
  double allowance=mode==2?.16:mode==1?.10:.075;
  return Math.Max(floor,Math.Min(1,1-Math.Max(0,slip-allowance)*(mode==2?1.2:2.5)));
 }
 public static double RollPower(double roll,int mode){
  if(mode==3)return 1;
  double risk=Math.Max(0,Math.Min(1,(Math.Abs(roll)-12)/15));
  return 1-risk*(mode==2?.10:mode==1?.22:.30);
 }
 public static double ExcessYaw(double yaw,double desired){
  double allowance=yaw*desired<0?0:Math.Abs(desired);
  return Math.Sign(yaw)*Math.Max(0,Math.Abs(yaw)-allowance-.12);
 }
 // For this rig, a brake on the left front creates negative yaw.
 public static double YawBrake(double error,double side,bool front,int mode){
  if(!front||mode==3||error*side>=0)return 0;
  return Math.Min(mode==2?.04:mode==1?.08:.12,Math.Abs(error)*.22);
 }

 public static double TractionBrake(double road,double tread,bool grounded,int mode){
  if(!grounded||mode>=2)return 0;
  double excess=Math.Abs(tread)-Math.Abs(road)-Math.Max(.20,Math.Abs(road)*(mode==0?.03:.05));
  return Math.Max(0,Math.Min(mode==0?.12:.08,excess*.04));
 }

}
public sealed class TractionCore {
 public double Scale=1;
 public double Step(double dt,bool enabled,double slip,bool grounded,int mode){
  if(!enabled||mode==3){Scale=1;return Scale;}
  double target=DriverAidsCore.Traction(slip,grounded,mode);
  dt=Math.Max(0,Math.Min(.1,dt));
  double rate=target<Scale?(mode==2?.5:1):.8;
  Scale+=Math.Max(-dt*rate,Math.Min(dt*rate,target-Scale));return Scale;
 }
}
// Independent actuator state so toggling one aid cannot leave the other's
// old brake pressure behind. No change to the existing pressure slew rates.
public sealed class AidBrakeCore {
 double traction,yaw;
 public void Reset(){traction=yaw=0;}
 static double Advance(double value,double target,double dt){
  target=Math.Max(0,Math.Min(1,target));
  return value+Math.Max(-dt*1.2,Math.Min(dt*.4,target-value));
 }
 public double Step(double dt,bool tractionEnabled,bool stabilityEnabled,double tractionTarget,double yawTarget){
  dt=Math.Max(0,Math.Min(.1,dt));
  traction=tractionEnabled?Advance(traction,tractionTarget,dt):0;
  yaw=stabilityEnabled?Advance(yaw,yawTarget,dt):0;
  return Math.Max(traction,yaw);
 }
}
public sealed class AntiLockCore {
 public double Scale=1;
 bool releasing;
 public double Step(double dt,bool enabled,bool grounded,double road,double tread,double requested){
  if(!enabled||!grounded||Math.Abs(road)<3||requested<=0){Scale=1;releasing=false;return Scale;}
  double slip=Math.Abs(road)-Math.Abs(tread);
  // This tire curve peaks in m/s, not a fixed percentage of vehicle speed.
  // At high speed, a percentage-only threshold would already be deep in slide.
  double release=Math.Min(Math.Abs(road)*.14,2.2),recover=Math.Min(Math.Abs(road)*.07,1.1);
  if(slip>release)releasing=true;else if(slip<recover)releasing=false;
  dt=Math.Max(0,Math.Min(.1,dt));Scale=Math.Max(0,Math.Min(1,Scale+dt*(releasing?-24:5)));
  return Scale;
 }
}
