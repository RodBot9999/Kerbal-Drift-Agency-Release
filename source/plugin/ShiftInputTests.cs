using System;
using KerbalDriftAgency;
class ShiftInputTests {
 static int checks;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 static GearboxCore Sequential(){return new GearboxCore{Automatic=false,ShiftSeconds=.06,MinimumShiftInterval=.12};}
 static void Tick(GearboxCore g,int frames,double wheel=100){for(int i=0;i<frames;i++)g.Step(.02,true,1,wheel,3000,6500);}
 static void Main(){
  var g=Sequential();Check(g.RequestManualShift(1,100,6500),"first shift immediate");
  Tick(g,4);Check(g.RequestManualShift(1,100,6500)&&g.Gear==2,"tap inside short cooldown buffered");
  Tick(g,3);Check(g.Gear==3&&g.ShiftSerial==2,"buffer executes once when ready");
  Tick(g,30);Check(g.Gear==3,"no autonomous repeated shifts");
  var burst=Sequential();burst.RequestManualShift(1,100,6500);for(int i=0;i<10;i++)burst.RequestManualShift(1,100,6500);
  Tick(burst,15);Check(burst.Gear==3&&burst.ShiftSerial==2,"one pending request rather than ten shifts");
  var manual=new GearboxCore{Automatic=false};manual.RequestManualShift(1,100,6500);Tick(manual,16);
  Check(manual.RequestManualShift(1,100,6500)&&manual.Gear==3,"manual bypasses automatic anti-hunting pause");
  var protect=Sequential();protect.Gear=3;protect.RequestManualShift(1,100,6500);Tick(protect,4);
  Check(protect.RequestManualShift(-1,100,6500),"safe downshift queues");Tick(protect,5,1200);
  Check(protect.Gear==4,"queued downshift rechecks increased wheel RPM");
  var rejected=Sequential();rejected.Gear=3;Check(!rejected.RequestManualShift(-1,1200,6500),"unsafe downshift rejected immediately");
  var neutral=Sequential();neutral.RequestManualShift(1,100,6500);neutral.RequestManualShift(1,100,6500);neutral.Select(0,0);Tick(neutral,20);
  Check(neutral.Gear==2&&neutral.Selector==0,"neutral cancels pending shift");
  var auto=new GearboxCore();auto.Step(.02,true,1,100,6400,6500);Tick(auto,16);
  Check(auto.RequestManualShift(1,100,6500)&&auto.ManualOverride,"automatic manual override responds after completed shift");
  Console.WriteLine("PASS "+checks+" shift-input checks");
 }
}

