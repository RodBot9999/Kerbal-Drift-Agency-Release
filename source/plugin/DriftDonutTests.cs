using System;
class DriftDonutTests {
 static int checks;static void Check(bool ok,string s){if(!ok)throw new Exception(s);checks++;}
 static DriftKeyboardBlend Held(double speed,int sign,double balance,double seconds){
  var k=new DriftKeyboardBlend();for(int i=0;i<(int)(seconds/.02);i++)k.Step(.02,true,sign,balance/79,64.0/79,0,balance/79,15.0/79,speed);return k;
 }
 static double Taps(int period,int on){var k=new DriftKeyboardBlend();for(int i=0;i<100;i++)k.Step(.02,true,i%period<on?-1:0,50.0/79,64.0/79,0,50.0/79,15.0/79,10);return Math.Abs(k.Bias);}
 static void Main(){
  var tap=Held(10,-1,50,.02);Check(Math.Abs(tap.Bias*79)<1.2,"Tap remains fine-grained");
  var slow=Held(8,-1,64,1);var fast=Held(30,-1,64,1);
  Check(Math.Abs(slow.Bias*79+35.2)<.001,"Donut hold trims up to 55 percent of SC lock");
  Check(Math.Abs(fast.Bias*79+22.4)<.001,"High speed authority stays smaller");
  Check(slow.Output>0&&Math.Abs(slow.Output*79-28.8)<.001,"A hold does not replace SC with opposite full lock");
  Check(Taps(20,1)<Taps(4,1)&&Taps(4,1)<Taps(2,1)&&Taps(2,1)<Taps(1,1),"Tap frequency gives progressive control");
  var saturated=Held(8,1,64,1);
  Check(Math.Abs(saturated.Output*79-79)<.001,"SC plus keyboard retains 15-degree physical reserve");
  Check(Math.Abs(saturated.Bias*79-15)<.001,"No invisible windup at physical lock");
  double before=saturated.Output;
  saturated.Step(.02,true,-1,64.0/79,64.0/79,0,before,15.0/79,8);
  Check(saturated.Output<before&&Math.Abs((saturated.Output-before)*79)<1.9,"Reverse key takes effect immediately but progressively");
  before=slow.Output;slow.Step(.02,true,0,64.0/79,64.0/79,0,before,15.0/79,8);
  Check(slow.Output>before&&Math.Abs((slow.Output-before)*79)<2.2,"Release smoothly restores controller balancing");
  for(int i=0;i<120;i++)slow.Step(.02,true,0,64.0/79,64.0/79,0,slow.Output,15.0/79,8);
  Check(Math.Abs(slow.Output*79-64)<.03,"Released keys return control to SC");
  foreach(int sign in new[]{-1,1})foreach(double lockDegrees in new[]{25.0,50.0,64.0,75.0}){
   double physical=Math.Max(60,lockDegrees+15),cap=lockDegrees/physical,extra=15/physical;
   var k=new DriftKeyboardBlend();
   for(int i=0;i<100;i++)k.Step(.02,true,sign,sign*cap,cap,0,sign*cap,extra,8);
   Check(Math.Abs(k.Output*physical-sign*(lockDegrees+15))<.001,"Both directions respect their separate SC/driver lock");
   for(int i=0;i<100;i++)k.Step(.02,true,-sign,sign*cap,cap,0,k.Output,extra,8);
   Check(Math.Abs(k.Output)<=cap+extra+1e-9,"Opposing hold stays within hardware lock");
  }
  for(int mode=0;mode<4;mode++)foreach(bool front in new[]{false,true}){
   double pressure=DriverAidsCore.ServiceBrake(.7,mode,front);
   Check(pressure==(mode==3&&!front?0:.7),"Front-only service braking applies only in Drift");
  }
  Check(DriverAidsCore.ServiceBrake(0,3,true)==0,"Brake release clears front service demand");
  Console.WriteLine("PASS: "+checks+" Drift donut authority/front service-brake checks");
 }
}
