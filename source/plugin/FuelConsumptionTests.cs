using System;using KerbalDriftAgency;
class FuelConsumptionTests {
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static double Rate(double rpm,double torque,double throttle,double load,bool neutral,double multiplier){return FuelConsumptionCore.UnitsPerSecond(true,false,rpm,torque,throttle,load,neutral,multiplier);}
 static void Main(){
  Check(Math.Abs(Rate(900,0,0,1,false,1)*5*3600-.8)<1e-10,"Idle is 0.8 kg/hour, not old 27 kg/hour");
  double idle=Rate(6000,0,1,1,false,1),a=Rate(6000,200,1,1,false,1),b=Rate(6000,400,1,1,false,1);
  Check(Math.Abs((b-idle)/(a-idle)-2)<1e-10,"Power-dependent fuel mass conserves energy");
  double expected=400*6000*Math.PI/30/(46400000*.3)/5+idle;
  Check(Math.Abs(b-expected)<1e-12,"W, J/kg and five kg/resource-unit conversion");
  Check(Rate(6000,400,1,1,true,1)<b*.1,"Neutral free revs do not burn full-load fuel");
  Check(Rate(6000,400,1,.03,false,1)<b*.1,"Airborne wheel load does not burn full-load fuel");
  foreach(double mult in new[]{.1,.5,1,2,3}){
   Check(Math.Abs(Rate(6000,400,1,1,false,mult)/b-mult)<1e-12,"Multiplier scales all consumption exactly");
   Check(FuelConsumptionCore.UnitsPerSecond(false,false,6000,400,1,1,false,mult)==0,"Stopped engine uses no fuel");
   Check(FuelConsumptionCore.UnitsPerSecond(true,true,6000,400,1,1,false,mult)==0,"Cut uses no fuel");
  }
  Check(FuelConsumptionCore.Multiplier(double.NaN)==1&&FuelConsumptionCore.Multiplier(double.PositiveInfinity)==1,"Invalid multiplier safe default");
  Check(FuelConsumptionCore.Multiplier(0)==.1&&FuelConsumptionCore.Multiplier(20)==3,"Multiplier bounds");
  var e=new EngineCore();e.FuelConsumptionMultiplier=.5;for(int i=0;i<200;i++)e.Step(.02,true,0,0);Check(e.FuelRate>0&&e.FuelRate<.00003,"Multiplier reaches live engine core idle");
  for(int i=0;i<300;i++)e.Step(.02,true,0,1000);Check(e.Rpm>e.IdleRpm*1.5&&e.FuelRate==0,"Coupled overrun fuel cut");
  e.GearRatio=0;for(int i=0;i<200;i++)e.Step(.02,true,0,1000);Check(e.FuelRate>0,"Neutral retains idle fuel");
  for(int i=0;i<300;i++)e.Step(.02,false,1,1000);Check(e.FuelRate==0,"Engine off consumes zero");
  Console.WriteLine("Fuel consumption: "+checks+" checks passed; 400 Nm at 6000 RPM = "+(b*5*3600).ToString("F2")+" kg/hour");
 }
}
