using System;
using KerbalDriftAgency;
class TuningCoreTests {
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Main(){
  foreach(int count in new[]{4,5,6}){
   var ratios=TuningCore.Ratios(count,3.7,300,8500,.296,3.89);
   double kmh=8500*2*Math.PI*.296*3.6/(60*ratios[count-1]*3.89);
   Check(ratios.Length==count&&Math.Abs(kmh-300)<1e-8,"generated "+count+"-speed top gear reaches requested gearing speed");
   Check(Math.Abs(ratios[0]/ratios[count-1]-3.7)<1e-8&&ratios[0]>ratios[1],"generated "+count+"-speed spacing and descending ratios");
  }
  var close=TuningCore.Ratios(5,2,300,6500,.296,3.89);var wide=TuningCore.Ratios(5,6,300,6500,.296,3.89);
  Check(Math.Abs(close[4]-wide[4])<1e-9&&wide[0]>close[0],"spacing changes lower gears without changing top speed");
  double neutral=TuningCore.Drag(1000,0,3.89,6500,900,0,false,2);
  double fifth=TuningCore.Drag(1000,.95,3.89,6500,900,0,true,2);
  double third=TuningCore.Drag(1000,1.65,3.89,6500,900,0,true,2);
  Check(third>fifth&&fifth>neutral&&neutral>0,"engine braking is stronger in lower gears; neutral retains rolling drag");
  Check(TuningCore.Drag(1000,.95,3.89,6500,900,1,true,2)==neutral,"engine braking disengages under throttle");
  Check(TuningCore.Drag(0,3.5,3.89,6500,900,0,true,2)==0,"drag is zero at standstill");
  Check(TuningCore.Drag(-1000,.95,3.89,6500,900,0,true,2)==fifth,"drag dissipates equally forward and reverse");
  var sequential=new GearboxCore();sequential.Automatic=false;sequential.ShiftSeconds=.06;sequential.MinimumShiftInterval=.12;
  Check(sequential.RequestShift(1,400,8500)&&sequential.TorqueFactor==0,"sequential shift starts with torque cut");
  for(int i=0;i<4;i++)sequential.Step(.02,true,1,400,3000,8500);
  Check(!sequential.Shifting&&sequential.TorqueFactor==1&&sequential.ShiftSerial==1,"sequential shift completes within 80 ms and records event");
  for(int i=0;i<5;i++)sequential.Step(.02,true,1,400,3000,8500);
  Check(!sequential.RequestShift(-1,1500,8500),"sequential unsafe downshift remains blocked");
  var high=new GearboxCore();high.Step(.02,true,1,500,6200,8500);
  Check(high.Gear==1,"higher redline allows automatic gearbox to hold beyond old 6000 RPM shift point");
 }
}
