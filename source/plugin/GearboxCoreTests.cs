using System;
using KerbalDriftAgency;
class GearboxCoreTests {
 static void Check(bool pass,string name){if(!pass)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Step(GearboxCore g,double rpm,double wheel,double throttle=1,double seconds=1){for(int i=0;i<seconds/.02;i++)g.Step(.02,true,throttle,wheel,rpm,6500);}
 static void Main(){
  var g=new GearboxCore();Step(g,6100,450,1,.02);Check(g.Gear==2&&g.TorqueFactor==0,"automatic upshift and torque cut");
  Step(g,3500,450,1,1);Check(g.TorqueFactor==1&&!g.Shifting,"shift completes and torque returns");
  g.Automatic=false;Check(g.RequestShift(1,450,6500)&&g.Gear==3,"manual upshift");
  Check(!g.RequestShift(1,450,6500),"shift delay prevents rapid repeated changes");Step(g,3500,450);
  Check(!g.RequestShift(-1,1000,6500),"unsafe downshift blocked");
  Check(g.RequestShift(-1,400,6500)&&g.Gear==2,"safe manual downshift");
  Check(!g.Select(-1,20),"reverse selection blocked while moving");
  Check(g.Select(0,20)&&g.TorqueFactor==0&&g.Ratio==0,"neutral disconnects drivetrain");
  Check(!g.Select(1,20),"moving neutral to drive blocked");
  Check(g.Select(-1,0)&&g.Display=="R"&&g.Ratio>0,"stationary reverse selection");
  g.Select(1,0);g.Gear=4;Check(g.Select(1,30)&&g.Gear==4,"reselecting Drive cannot force first gear");
  g.Automatic=true;Step(g,6000,400,.5,5);Check(g.Gear==5,"automatic reaches top gear");
  Step(g,1200,60,0,5);Check(g.Gear==1,"automatic returns to first as speed falls");
 }
}
