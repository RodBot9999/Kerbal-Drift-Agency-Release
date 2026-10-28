using System;
using KerbalDriftAgency;
class RefinementsTests {
 static void Check(bool c,string s){if(!c)throw new Exception(s);Console.WriteLine("PASS "+s);}
 static void GearStep(GearboxCore g,double seconds,double rpm=5000){for(int i=0;i<seconds/.02;i++)g.Step(.02,true,1,200,rpm,6500);}
 static void Main(){
  var g=new GearboxCore();Check(g.RequestManualShift(1,200,6500)&&g.Gear==2&&g.Automatic&&g.ManualOverride,"automatic accepts a manual shift without changing gearbox setup");
  GearStep(g,2,6100);Check(g.Gear==2&&g.ManualOverride,"automatic holds the requested gear below protection RPM");
  GearStep(g,7,5000);Check(!g.ManualOverride&&g.Automatic,"automatic resumes after the override timeout");
  Check(!g.RequestManualShift(-1,1000,6500),"Tiptronic override still rejects unsafe downshifts");
  g.RequestManualShift(1,100,6500);g.ResumeAutomatic();Check(!g.ManualOverride,"resume action immediately clears the override");
  var safety=new GearboxCore();safety.RequestManualShift(1,200,6500);GearStep(safety,1,6450);Check(safety.Gear>2,"override permits a protective upshift near the limiter");
  var heat=new TireHeatCore();double cold=heat.SmokeMultiplier;for(int i=0;i<1000;i++)heat.Step(.02,1);Check(heat.Temperature>100&&heat.SmokeMultiplier>cold*2.5,"sustained slip heats cosmetic tire temperature and substantially increases smoke");
  double hot=heat.Temperature;for(int i=0;i<3000;i++)heat.Step(.02,0);Check(heat.Temperature<hot*.6&&heat.Temperature>=25,"tires cool gradually toward ambient after slip ends");
  Check(TireSmokeCore.Strength(false,30,0,0)==0,"hot airborne tire still requires contact for smoke");
  double low=TuningCore.Drag(2500/(3.5*3.89),3.5,3.89,6500,900,0,true,2);
  double high=TuningCore.Drag(6000/(3.5*3.89),3.5,3.89,6500,900,0,true,2);
  Check(high>low*2,"high RPM produces substantially more engine braking in the same gear");
  Check(TuningCore.Drag(6000/(3.5*3.89),3.5,3.89,6500,900,1,true,2)<high*.2,"engine braking disengages under throttle");
  var f=new ExhaustCore();f.Step(.02,true,1,5000,0,3,true,1,.1);
  Check(f.Step(.02,true,0,5000,0,3,true,.8,.1),"high RPM lift can trigger an exhaust flame");
  Check(!f.Step(.02,true,0,5000,0,3,true,.7,.1),"exhaust cooldown prevents continuous frame-by-frame flames");
  var down=new ExhaustCore();down.Step(.02,true,0,4000,0,3,false,0,.1);Check(down.Step(.02,true,0,4500,1,2,false,0,.1),"downshift can trigger a flame without a big turbo");
  var up=new ExhaustCore();up.Step(.02,true,1,5500,0,2,true,1,.1);Check(up.Step(.02,true,1,4500,1,3,true,.8,.1),"boosted big turbo upshift can trigger a flame");
  var off=new ExhaustCore();off.Step(.02,false,1,5000,0,3,true,1,.1);Check(!off.Step(.02,false,0,5000,1,2,true,1,.1),"engine off suppresses exhaust flames");
  var odds=new ExhaustCore();odds.Step(.02,true,1,5000,0,3,true,1,.99);Check(!odds.Step(.02,true,0,5000,0,3,true,1,.99),"flames are occasional rather than guaranteed");
 }
}
