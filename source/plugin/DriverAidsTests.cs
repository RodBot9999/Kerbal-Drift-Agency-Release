using System;
class DriverAidsTests {
 static int checks;static void Check(bool v,string name){if(!v)throw new Exception(name);checks++;}
 static void Main(){
  Check(DriverAidsCore.Traction(.04,true)==1,"rolling torque preserved");Check(DriverAidsCore.Traction(.3,true)<.2,"strong spin reduction");Check(DriverAidsCore.Traction(1,true)==.02,"high slip torque floor");Check(DriverAidsCore.Traction(0,false)==0,"airborne TCS");
  var abs=new AntiLockCore();for(int i=0;i<4;i++)abs.Step(.02,true,true,30,0,1);
  Check(abs.Scale==0,"locked wheel releases brakes");
  for(int i=0;i<10;i++)abs.Step(.02,true,true,30,29,1);
  Check(abs.Scale>.9,"rolling wheel restores brakes");
  Check(abs.Step(.02,false,true,30,0,1)==1,"disabled ABS full braking");Check(abs.Step(.02,true,true,2,0,1)==1,"low speed stopping");Check(abs.Step(.02,true,true,-30,-29,1)==1,"reverse rolling");
  abs.Step(.02,true,true,-30,0,1);Check(abs.Scale<1,"reverse lock releases");Check(abs.Step(.02,true,false,30,0,1)==1,"airborne bypass");
  Check(abs.Step(.02,true,true,55,52,1)<1,"high-speed ABS uses native slip-speed curve");
  var r=KDACore.Evaluate(2,1,5,55,0,40,2.15,9.81,25,.2,0,0,true,false);
  Check(r.RecoveryLimit>r.Limit*10,"drift recovery has extra authority");Check(Math.Abs(r.Steering)>r.Limit,"countersteer escapes normal cornering cap");Check(Math.Abs(r.Steering)*40<=32,"recovery angle bounded");Check(r.Throttle==0,"large slide full throttle cut");
  var road=KDACore.Preset(0);Check(road.FrontSpring==112.5&&road.RearSpring==103.125,"road additional 25 percent");Check(KDACore.Preset(2).FrontSpring==263.25,"race rate not clamped by old maximum");
  var low=KDACore.Evaluate(2,1,0,4,1,20,2.15,9.81,-30,2,0,0,true,false);Check(Math.Abs(low.Steering)<=1,"recovery respects normalized physical input range");
  Console.WriteLine(checks+" driver-aid checks passed");
 }
}

