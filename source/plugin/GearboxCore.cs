using System;
namespace KerbalDriftAgency {
 public sealed class GearboxCore {
  public double[] Ratios={3.50,2.25,1.65,1.25,.95};
  public double ReverseRatio=3.4,FinalDrive=3.89,ShiftSeconds=.30,MinimumShiftInterval=.85;
  public int Gear=1,Selector=1;
  public bool Automatic=true;
  double remaining,cooldown;
  double manualCooldown,pendingSeconds;int pendingManual;
  double manualHold;
  public bool ManualOverride {get{return Automatic&&manualHold>0;}}
  public void ResumeAutomatic(){manualHold=0;pendingManual=0;}
  public bool RequestManualShift(int change,double wheelRpm,double limit){
   change=Math.Sign(change);
   if(!SafeNext(change,wheelRpm,limit))return false;
   // One short-lived request, never an accumulated burst of held-key shifts.
   // Ignore automatic anti-hunting cooldown for a deliberate driver request.
   if(Shifting||manualCooldown>0){
    if(Math.Max(remaining,manualCooldown)>.25)return false;
    pendingManual=change;pendingSeconds=.25;
   }else StartShift(change);
   if(Automatic)manualHold=8;return true;
  }
  public bool Shifting {get{return remaining>0;}}
  public double Ratio {get{return Selector<0?ReverseRatio:Selector==0?0:Ratios[Gear-1];}}
  public double TorqueFactor {get{return Selector==0?0:remaining>0?EngineCore.Clamp(1-remaining/Math.Min(.10,ShiftSeconds/3),0,1):1;}}
  public int ShiftSerial {get;private set;}
  public string Display {get{return Selector<0?"R":Selector==0?"N":Gear.ToString();}}
  public bool Select(int selector,double speed){
   selector=Math.Sign(selector);
   if(selector==Selector)return true;
   if(selector!=0 && selector!=Selector && speed>1)return false;
   Selector=selector;remaining=0;cooldown=MinimumShiftInterval;manualHold=0;manualCooldown=0;pendingManual=0;
   if(selector==1)Gear=1;
   return true;
  }
  public bool RequestShift(int change,double wheelRpm,double limit){
   if(Selector!=1 || Shifting || cooldown>0)return false;
   if(!SafeNext(change,wheelRpm,limit))return false;
   StartShift(change);return true;
  }
  bool SafeNext(int change,double wheelRpm,double limit){
   if(Selector!=1||change==0)return false;
   int next=Math.Max(1,Math.Min(Ratios.Length,Gear+Math.Sign(change)));
   if(next==Gear)return false;
   if(change<0 && Math.Abs(wheelRpm)*Ratios[next-1]*FinalDrive>limit-250)return false;
   return true;
  }
  void StartShift(int change){
   Gear+=Math.Sign(change);remaining=Math.Max(.02,ShiftSeconds);
   cooldown=Math.Max(ShiftSeconds,MinimumShiftInterval);
   manualCooldown=Math.Max(remaining,Math.Min(.12,MinimumShiftInterval));
   pendingManual=0;ShiftSerial++;
  }
  public void Step(double dt,bool running,double throttle,double wheelRpm,double engineRpm,double limit){
   dt=EngineCore.Clamp(dt,0,.1);remaining=Math.Max(0,remaining-dt);cooldown=Math.Max(0,cooldown-dt);
   manualCooldown=Math.Max(0,manualCooldown-dt);pendingSeconds=Math.Max(0,pendingSeconds-dt);
   manualHold=Math.Max(0,manualHold-dt);
   Gear=Math.Max(1,Math.Min(Ratios.Length,Gear));
   if(pendingManual!=0){
    if(!running||Selector!=1||pendingSeconds<=0)pendingManual=0;
    else if(!Shifting&&manualCooldown<=0){
     int request=pendingManual;pendingManual=0;
     // Recheck current wheel RPM at execution; a buffered downshift must
     // never bypass over-rev protection after the vehicle accelerates.
     if(SafeNext(request,wheelRpm,limit)){StartShift(request);if(Automatic)manualHold=8;}
    }
   }
   if(!running || !Automatic || Selector!=1 || Shifting || cooldown>0)return;
   if(ManualOverride&&engineRpm<limit-100)return;
   double up=limit*(.53846+.38462*EngineCore.Clamp(Math.Abs(throttle),0,1));
   up=Math.Min(up,limit-350);
   if(engineRpm>up && Gear<Ratios.Length){RequestShift(1,wheelRpm,limit);return;}
   double down=Math.Abs(throttle)>.75?2600:1700;
   if(engineRpm<down && Gear>1)RequestShift(-1,wheelRpm,limit);
  }
 }
}
