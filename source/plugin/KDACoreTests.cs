using System;
class KDACoreTests {
 static int n;static void Check(bool ok,string name){if(!ok)throw new Exception(name);n++;}
 static KDACore.Result Run(double speed,double steer,double slip,double yaw,double roll,double unload,bool ground,bool burnout,double strength){return KDACore.Evaluate(1,strength,5,speed,steer,4,2.15,9.81,slip,yaw,roll,unload,ground,burnout);}
 static void Main(){
  var road=KDACore.Preset(0);var sport=KDACore.Preset(1);var race=KDACore.Preset(2);
  Check(road.Grip<sport.Grip&&sport.Grip<race.Grip,"grip progression");Check(road.Height>sport.Height&&sport.Height>race.Height,"height progression");Check(race.FrontSpring>sport.FrontSpring&&sport.FrontSpring>road.FrontSpring,"spring progression");
  Check(race.RearBar<race.FrontBar,"rear bar conservative");
  Check(Run(2,1,20,2,20,1,true,false,1).Steering==1,"low-speed bypass");Check(Run(30,1,20,2,20,1,false,false,1).Steering==1,"airborne bypass");Check(Run(30,1,20,2,20,1,true,false,0).Throttle==1,"disabled bypass");
  var fast=Run(50,1,0,0,0,0,true,false,1);Check(fast.Limit<.2,"speed steering limit");
  var highway=KDACore.Evaluate(0,1,3,60,1,20,2.15,9.81,0,0,0,0,true,false);Check(highway.Limit<.01,"no unsafe minimum steering floor at 216 kmh");
  Check(Run(30,0,0,.025,0,0,true,false,1).Correction==0,"yaw noise deadband");
  Check(Run(30,.5,Double.NaN,0,0,0,true,false,1).Steering==.5,"invalid telemetry bypass");
  Check(Run(30,Double.NaN,0,0,0,0,true,false,1).Steering==0,"invalid input sanitized");
  var slide=Run(30,0,15,0,0,0,true,false,1);Check(slide.Steering<0&&slide.Throttle<1,"rightward slide countersteer right");
  Check(Run(30,0,-15,0,0,0,true,false,1).Steering>0,"leftward slide countersteer left");
  Check(Run(30,0,0,1,0,0,true,false,1).Steering>0,"excess right yaw counters left");
  Check(Run(30,0,0,0,18,.8,true,false,1).Throttle<.4,"roll and unloading power reduction");
  Check(Run(30,0,15,1,18,.8,true,true,1).Throttle==1,"intentional burnout power retained");
  var burnout=Run(30,0,15,1,18,.8,true,true,1);Check(burnout.Correction==0,"burnout drift correction bypass");
  for(int mode=0;mode<3;mode++)for(int speed=4;speed<85;speed+=4){var r=KDACore.Evaluate(mode,1,5,speed,1,20,2.15,9.81,30,2,30,1,true,false);Check(Math.Abs(r.Steering)<=1&&r.Throttle>=0&&r.Throttle<=1,"bounded output");}
  Console.WriteLine(n+" KDA checks passed");
 }
}
