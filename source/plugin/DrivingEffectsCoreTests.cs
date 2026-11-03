using System;
using KerbalDriftAgency;
class DrivingEffectsCoreTests {
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Main(){
  var b=new BurnoutCore();Check(b.Step(true,true,true,true,true,0),"burnout starts stationary in Drive");
  Check(b.Step(true,true,true,true,true,2),"burnout speed hysteresis allows minor creep");
  Check(!b.Step(true,true,true,true,true,3),"burnout exits on excess speed");
  Check(!b.Step(true,true,true,true,true,2),"burnout cannot start at driving speed");
  b.Step(true,true,true,true,true,0);Check(!b.Step(false,true,true,true,true,0),"releasing either pedal exits");
  Check(!b.Step(true,true,false,true,true,0),"engine off blocks burnout");
  Check(!b.Step(true,true,true,false,true,0),"neutral and reverse block burnout");
  Check(!b.Step(true,true,true,true,false,0),"airborne rear wheels block burnout");
  Check(!b.Step(true,false,true,true,true,0),"UI or inactive-vessel lock blocks burnout");
  Check(TireSmokeCore.Strength(true,20,0,0)>0,"spinning grounded tire emits smoke");
  Check(TireSmokeCore.Strength(true,0,20,0)>0,"locked tire skid emits smoke");
  Check(TireSmokeCore.Strength(true,20,20,6)>0,"lateral slip emits smoke");
  Check(TireSmokeCore.Strength(true,20,20,0)==0,"normal rolling emits no smoke");
  Check(TireSmokeCore.Strength(false,100,0,0)==0,"airborne wheel emits no smoke");
  Check(TireSmokeCore.Strength(true,1,0,0)==0,"small low-speed slip emits no smoke");
 }
}
