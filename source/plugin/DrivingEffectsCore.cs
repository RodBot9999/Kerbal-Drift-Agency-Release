using System;
namespace KerbalDriftAgency {
 public sealed class BurnoutCore {
  public bool Active {get;private set;}
  public bool Step(bool both,bool available,bool running,bool drive,bool grounded,double speed){
   Active=both&&available&&running&&drive&&grounded&&Math.Abs(speed)<(Active?3:1.5);
   return Active;
  }
 }
 public static class TireSmokeCore {
  public static double Strength(bool grounded,double tread,double forward,double lateral){
   if(!grounded||Math.Max(Math.Abs(tread),Math.Max(Math.Abs(forward),Math.Abs(lateral)))<4)return 0;
   double slip=Math.Abs(tread-forward)+.6*Math.Abs(lateral);
   return EngineCore.Clamp((slip-2)/12,0,1);
  }
 }
 // Cosmetic tire heat only: no effect on grip, wear or KSP part temperature.
 public sealed class TireHeatCore {
  public double Temperature=25;
  public double SmokeMultiplier {get{return 1+2.5*EngineCore.Clamp((Temperature-40)/120,0,1);}}
  public void Step(double dt,double slip){
   dt=EngineCore.Clamp(dt,0,.1);
   Temperature+=((180-Temperature)*EngineCore.Clamp(slip,0,1)/12-(Temperature-25)/45)*dt;
   Temperature=EngineCore.Clamp(Temperature,25,180);
  }
 }
 // Visual-only envelope: brief tire scrub stays subtle; held slides build a trail.
 public sealed class TireSmokeEnvelope {
  public double Strength,TrailBuild;public bool Burnout,RollingBurnout;
  public void Step(double dt,bool grounded,double tread,double forward,double lateral){
   dt=EngineCore.Clamp(dt,0,.1);
   if(!grounded){Strength=0;TrailBuild=0;Burnout=RollingBurnout=false;return;}
   double speed=Math.Sqrt(forward*forward+lateral*lateral);
   // Imported wheel axes can have opposite signs. Clean rolling must not
   // become smoke just because a left/right wheel uses the other convention.
   double slip=Math.Abs(Math.Abs(tread)-Math.Abs(forward));
   Burnout=speed<6&&Math.Abs(tread)>7&&slip>5;
   RollingBurnout=speed>=6&&Math.Abs(tread)>Math.Abs(forward)+7&&Math.Abs(lateral)<Math.Max(2.5,Math.Abs(forward)*.22);
   double angle=Math.Atan2(Math.Abs(lateral),Math.Max(2,Math.Abs(forward)))*180/Math.PI;
   double side=EngineCore.Clamp((angle-4)/12,0,1)*EngineCore.Clamp((Math.Abs(lateral)-2)/8,0,1);
   double spin=EngineCore.Clamp((slip-3)/15,0,1);
   double active=Math.Max(side,spin);
   TrailBuild=EngineCore.Clamp(TrailBuild+dt*(active>.2?1.1:-2),0,1);
   double target=active*(Burnout?.85:(.08+.92*TrailBuild));
   Strength+=(target-Strength)*(1-Math.Exp(-dt*(target>Strength?5:10)));
  }
 }
 public static class GroundSmokeMotion {
  // Two populations share the same particle budget: a tire/body cloud and
  // a detached rearward plume. Velocities are ground-relative, never wind
  // gusts or a car-attached wall. Initial wake energy dissipates with age.
  public static double RearwardSpeed(double roadSpeed,bool nearWheel){return nearWheel?.18:1.15+Math.Min(2.1,Math.Abs(roadSpeed)*.12);}
  public static double RearwardTarget(double initial,double age,bool nearWheel){return initial*Math.Exp(-Math.Max(0,age)/(nearWheel?.65:2.5));}
  public static double WakeCarry(double roadSpeed){return Math.Min(5,Math.Abs(roadSpeed)*.32);}
  // Cosmetic buoyancy. Only newly emitted burnout puffs use the tire heat;
  // hot tires on a moving car still leave the original low drift trail.
  public static double HeatLift(double temperature){return EngineCore.Clamp((temperature-70)/75,0,1);}
  public static double TireWindSpeed(double ambientSpeed){return Math.Min(.45,Math.Abs(ambientSpeed)*.12);}
  public static double Vertical(double height,double velocity,double age,bool burnout,double dt){
   return Vertical(height,velocity,age,burnout,dt,0);
  }
  public static double Vertical(double height,double velocity,double age,bool burnout,double dt,double heatLift){
   return Vertical(height,velocity,age,burnout,dt,heatLift,false);
  }
  public static double Vertical(double height,double velocity,double age,bool burnout,double dt,double heatLift,bool nearWheel){
   dt=EngineCore.Clamp(dt,0,.05);
   heatLift=burnout?EngineCore.Clamp(heatLift,0,1):0;
   // Sustained hot burnouts can build above the roof. Cold puffs retain
   // their modest rise; drift puffs keep the established low blanket.
   double ceiling=burnout?(nearWheel?.7+.45*heatLift:2.3+2.5*heatLift):.65;
   double desired=burnout?(nearWheel?.07+.12*heatLift:age<.8?.2+.35*heatLift:.29+.55*heatLift):.035;
   double shoulder=burnout?(nearWheel?.25+.25*heatLift:.7+2.5*heatLift):.18;
   desired-=Math.Max(0,height-shoulder)*(burnout?(nearWheel?1.1:.55):1.4);
   velocity+=(desired-velocity)*(1-Math.Exp(-dt*4));
   if(height>=ceiling)velocity=Math.Min(velocity,-.08);
   if(height<=.065)velocity=Math.Max(velocity,0);
   return EngineCore.Clamp(velocity,-.45,burnout?.5+.65*heatLift:.12);
  }
 }
}
