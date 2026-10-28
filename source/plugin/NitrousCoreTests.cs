using System;using KerbalDriftAgency;
class NitrousCoreTests {
 static int checks;static void Check(bool pass,string name){checks++;if(!pass)throw new Exception(name);}
 static void Main(){
  var n=new NitrousCore();double torque=n.Step(.02,true,0,100,true,true,false,3000);
  Check(Math.Abs(torque*3000*Math.PI/30/745.699872-100)<1e-8,"shot is actual added hp");Check(n.Active&&n.Charge<1,"sprays and consumes");
  for(int i=0;i<500;i++){torque=n.Step(.02,true,0,100,true,true,false,3000);Check(torque>=0&&n.Charge>=0&&n.Charge<=1,"bounded bottle");}
  Check(!n.Active&&n.Charge==0,"empty bottle cuts power");n.Step(.1,true,0,100,true,true,false,3000);Check(n.Charge==0,"holding empty bottle does not auto recharge");
  n.Step(.1,true,0,100,false,true,false,3000);Check(n.Charge>0&&!n.Active,"release recharges");
  n.Charge=1;double start=n.Charge;torque=n.Step(.1,true,1,100,true,false,false,3000);Check(torque==0&&n.Charge==start,"interlock consumes nothing");
  n.Charge=.5;n.Step(.1,false,1,100,false,true,true,3000);Check(n.Charge==.5&&!n.Active,"uninstalled kit inert");n.Step(0,true,1,100,true,true,true,3000);Check(n.Charge==.5&&!n.Active,"paused step inert");
  n.Charge=.5;for(int i=0;i<6;i++)n.Step(.1,true,1,100,false,true,true,3000);Check(n.DriftRecharge,"sustained drift qualifies");
  double before=n.Charge;n.Step(.1,true,1,100,false,true,true,3000);double drift=n.Charge-before;
  before=n.Charge;n.Step(.1,true,1,100,false,true,false,3000);Check(!n.DriftRecharge&&Math.Abs(drift/(n.Charge-before)-3)<1e-8,"drift recharge exactly 3x");
  n.Charge=.0001;torque=n.Step(.1,true,0,300,true,true,false,3000);Check(torque>0&&torque<237.36&&n.Charge==0,"partial last shot scales torque");
  for(int bottle=0;bottle<3;bottle++){n=new NitrousCore();for(int i=0;i<100;i++)n.Step(.02,true,bottle,100,true,true,false,3000);Check(Math.Abs(n.Charge-(1-2/NitrousCore.Capacity(bottle)))<1e-8,"three bottle capacities");}
  n.Charge=double.NaN;torque=n.Step(double.NaN,true,99,double.NaN,true,true,false,double.NaN);Check(!double.IsNaN(torque)&&!double.IsNaN(n.Charge),"corrupt state sanitized");
  Console.WriteLine("Nitrous checks passed: "+checks);
 }
}
