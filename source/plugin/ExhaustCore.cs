using System;
namespace KerbalDriftAgency {
 public sealed class ExhaustCore {
  double previousThrottle,window,cooldown;int previousSerial,previousGear;bool initialized;
  public bool Step(double dt,bool running,double throttle,double rpm,int serial,int gear,bool bigTurbo,double boost,double chance,double multiplier=1,int tune=0){
   multiplier=double.IsNaN(multiplier)?1:EngineCore.Clamp(multiplier,0,3);tune=Math.Max(0,Math.Min(2,tune));
   dt=EngineCore.Clamp(dt,0,.1);cooldown=Math.Max(0,cooldown-dt);window=Math.Max(0,window-dt);
   if(!initialized){previousSerial=serial;previousGear=gear;previousThrottle=throttle;initialized=true;return false;}
   bool shifted=serial!=previousSerial,down=gear<previousGear;
   bool lift=previousThrottle>.3&&throttle<.1;
   previousSerial=serial;previousGear=gear;previousThrottle=throttle;
   if(!running||multiplier<=0){window=0;return false;}
   if(lift&&rpm>3500)window=tune==0?1.8:3;
   bool candidate=shifted&&rpm>3000&&(down||(bigTurbo&&boost>.5));
   bool decel=window>0&&throttle<.1&&rpm>3000;
   if(cooldown>0||(!candidate&&!decel))return false;
   cooldown=(candidate?.30:.45)/Math.Max(.25,multiplier)*(tune==0?1:.8);
   return chance<Math.Min(.98,(candidate?.75:.45)*(tune==0?1:1.35)*Math.Min(1,multiplier));
  }
 }
}
