using System;using KerbalDriftAgency;
class SmokeBehaviorTests{
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static double Height(bool burnout,double heat,double seconds){double height=.08,velocity=.12;for(double age=0;age<seconds;age+=.02){velocity=GroundSmokeMotion.Vertical(height,velocity,age,burnout,.02,heat);height+=velocity*.02;CheckFinite(height);}return height;}
 static void CheckFinite(double value){if(double.IsNaN(value)||double.IsInfinity(value))throw new Exception("non-finite smoke height");}
 static void Main(){
  Check(GroundSmokeMotion.TireWindSpeed(0)==0,"still air adds no drift");
  Check(GroundSmokeMotion.TireWindSpeed(3)<.4,"normal GroundFX wind becomes a gentle breeze");
  Check(GroundSmokeMotion.TireWindSpeed(12)<=.45&&GroundSmokeMotion.TireWindSpeed(100)<=.45,"strong winds cannot sweep tire clouds away");
  Check(GroundSmokeMotion.HeatLift(25)==0&&GroundSmokeMotion.HeatLift(70)==0,"cold tires do not produce tall plumes immediately");
  Check(GroundSmokeMotion.HeatLift(110)>.4&&GroundSmokeMotion.HeatLift(145)==1,"buoyancy grows progressively with cosmetic tire temperature");
  var tire=new TireHeatCore();for(int i=0;i<1000;i++)tire.Step(.02,1);double hot=GroundSmokeMotion.HeatLift(tire.Temperature);
  Check(hot>.7,"sustained burnout reaches stronger loft without changing physical tire grip");
  double coldH=Height(true,0,6),hotH=Height(true,hot,8),maxH=Height(true,1,8);
  Console.WriteLine("Cold height="+coldH.ToString("F2")+" m; 20-second hot tire="+tire.Temperature.ToString("F1")+" C; plume center="+hotH.ToString("F2")+" m; maximum heat center="+maxH.ToString("F2")+" m");
  Check(coldH<1.3&&hotH>2.7&&maxH<4.9,"hot burnout builds above the roof while staying bounded");
  Check(Math.Abs(Height(false,0,8)-Height(false,1,8))<1e-9,"hot moving drift retains the low smoke profile");
  double v=GroundSmokeMotion.Vertical(6,1,1,true,.02,1);Check(v<0,"hot smoke above its ceiling settles");
  var envelope=new TireSmokeEnvelope();for(int i=0;i<100;i++)envelope.Step(.02,true,40,0,0);envelope.Step(.02,false,40,0,0);Check(envelope.Strength==0&&!envelope.Burnout,"jump still cuts emission immediately");
 }
}
