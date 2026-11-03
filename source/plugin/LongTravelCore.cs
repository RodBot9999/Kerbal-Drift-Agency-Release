using System;
// Units: metres, tonnes, kN, kN/m (= N/mm). No Unity or vehicle identity.
public static class LongTravelCore {
 public static double Clamp(double x,double a,double b){return double.IsNaN(x)||double.IsInfinity(x)?a:Math.Max(a,Math.Min(b,x));}
 public static double Travel(int kit,double mm){return kit==0?.16:Clamp(mm,200,700)*.001;}
 public static double Rate(int kit,double rate){return Clamp(rate,kit==0?93.75:5,280);}
 public static double Height(int kit,double mm){return Clamp(mm,-20,kit==0?30:350);}
 public static double StaticDepth(double load,double spring,double travel){return .1*travel+Math.Max(0,load)/Math.Max(1,spring);}
 public static double Reference(double height,double load,double spring,double travel){return height*.001+load/Math.Max(1,spring)+travel*(.1-.25);}
 // Recommendation only: rough-ground support reserve above the parked load.
 // The input is a static sag ceiling; StaticDepth still reports true sag.
 public const double OffRoadLoadReserve=1.75;
 public static double SuggestedSpring(double load,double travel,double sagPercent){return Clamp(OffRoadLoadReserve*load/(travel*(Clamp(sagPercent,20,55)*.01-.1)),5,280);}
 public static double BumpForce(double spring,double travel,double depth,double startPercent,double strength){double x=Clamp((depth/travel-Clamp(startPercent,60,95)*.01)/(1-Clamp(startPercent,60,95)*.01),0,1);return spring*travel*.5*Clamp(strength,0,6)*x*x*x;}
 public static double EffectiveRate(double spring,double travel,double depth,double bumpForce){return spring+Math.Max(0,bumpForce)/Math.Max(.02,depth-.1*travel);}
 public static double DampingGain(double travel,double depth,double velocity,double bypass,double droop){double q=Clamp(depth/travel,0,1);double end=Clamp((q-.60)/.40,0,1),top=Clamp((.18-q)/.18,0,1);return 1+Clamp(bypass,0,3)*end*end+(velocity<0?Clamp(droop,0,3)*top*top:0);}
 public static double ArmLength(double travel){return Math.Max(.40,travel*.80+.10);}
 public static double Lateral(double travel,double heightMm,double staticDepth,double compression,double trackMm){double width=Clamp(trackMm,0,350)*.001,length=ArmLength(travel)+width,d=heightMm*.001+staticDepth-compression;d=Clamp(d,-length*.96,length*.96);return width+Math.Sqrt(Math.Max(0,length*length-d*d))-length;}
 public static double BarCap(double leftSpring,double rightSpring,double travel,double leftLoad,double rightLoad){return Math.Min(SuspensionCore.BarCapacity(leftSpring,rightSpring,travel),Math.Max(0,Math.Min(leftLoad,rightLoad))*.45);}
}
public sealed class SuspensionTravelMonitor {
 bool initialized,previousGrounded,bottom;double airborne;
 public int BottomingEvents {get;private set;}public int Landings {get;private set;}public double Airtime {get{return airborne;}}public double LastAirtime {get;private set;}public double LandingSpeed {get;private set;}
 public void Step(double dt,bool grounded,double fraction,double previousDownSpeed){dt=LongTravelCore.Clamp(dt,0,.1);if(!initialized){initialized=true;previousGrounded=grounded;}
  if(!grounded)airborne+=dt;else{if(!previousGrounded&&airborne>=.04){Landings++;LastAirtime=airborne;LandingSpeed=Math.Max(0,previousDownSpeed);}airborne=0;}
  if(grounded&&fraction>=.97&&!bottom){BottomingEvents++;bottom=true;}if(!grounded||fraction<.90)bottom=false;previousGrounded=grounded;
 }
}
