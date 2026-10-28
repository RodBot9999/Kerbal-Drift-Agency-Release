using System;
class StabilityObserverTests {
 static int checks;static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
 static void Main(){
  for(int mode=0;mode<3;mode++){
   var o=new StabilityObserver();double tolerance=mode==0?3:mode==1?5:8;
   for(int i=0;i<1000;i++){o.Step(.02,.25*Math.Sin(i*.9),.07*Math.Sin(i*.7),0,tolerance);var r=KDACore.Evaluate(mode,1,tolerance,55,0,40,2.15,9.81,o.Slip,o.Yaw,.5,.1,true,false);Check(!o.Active&&r.Correction==0,"straight noise never steers");}
  }
  var gate=new StabilityObserver();for(int i=0;i<3;i++)gate.Step(.02,0,.3,0,5);Check(!gate.Active,"short yaw spike ignored");
  gate.Reset();for(int i=0;i<15;i++)gate.Step(.02,0,.3,0,5);Check(gate.Active,"sustained yaw activates");
  gate.Reset();gate.Step(.02,25,0,0,5);gate.Step(.02,25,0,0,5);Check(gate.Active,"large slide activates rapidly");
  for(int i=0;i<60;i++)gate.Step(.02,0,0,0,5);Check(!gate.Active,"settled slide releases");
  gate.Reset();for(int i=0;i<100;i++)gate.Step(.02,2,.2,.2,5);Check(!gate.Active,"intended cornering yaw accepted");
  var under=KDACore.Evaluate(1,1,5,30,.1,40,2.15,9.81,0,0,0,0,true,false);Check(under.Correction==0,"no yaw tracking boost for normal steering ramp");
  gate.Step(.02,Double.NaN,0,0,5);Check(!gate.Active,"bad telemetry resets observer");
  Console.WriteLine(checks+" stability-observer checks passed");
 }
}
