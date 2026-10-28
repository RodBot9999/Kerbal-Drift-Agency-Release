using System;
class DriftTransitionTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static double Taps(int period,int on){var k=new DriftKeyboardBlend();for(int i=0;i<100;i++)k.Step(.02,true,i%period<on?1:0,-.4,.833,0,-.4);return k.Bias;}
 static void Main(){
  var intent=new DriftSteeringIntent();intent.Step(.02,3,true);
  for(int i=0;i<15;i++)intent.Step(.02,3,false);
  Check(intent.Active,"Zero-slip transition hold");
  for(int i=0;i<10;i++)intent.Step(.02,3,false);
  Check(!intent.Active,"Transition hold expires");
  for(int mode=0;mode<3;mode++){intent.Step(.02,3,true);intent.Step(.02,mode,true);Check(!intent.Active,"Other modes isolated");}
  var k=new DriftKeyboardBlend();double first=k.Step(.02,true,1,-.4,.833,0,-.4);
  Check(first>-.4&&first<-.37,"Single tap adjusts balance by less than two degrees");
  double bias=k.Bias;double released=k.Step(.02,true,0,-.4,.833,0,first);
  Check(k.Bias>bias*.9&&released<first&&released>-.4,"Tap fades gradually instead of resetting");
  double sparse=Taps(20,1),frequent=Taps(4,1),dense=Taps(2,1),held=Taps(1,1);
  Check(sparse<frequent&&frequent<dense&&dense<=held,"Tap duty cycle controls accumulated steering");
  Check(held<=.833*.35+1e-9,"Hold remains a bounded adjustment, not full wheel lock");
  for(int i=0;i<100;i++)k.Step(.02,true,1,-.4,.833,0,k.Output);
  Check(k.Output<0,"Reducing countersteer retains natural slide instead of forcing opposite steering");
  double previous=k.Output;
  for(int i=0;i<40;i++){double next=k.Step(.02,true,-1,-.4,.833,0,previous);Check(Math.Abs(next-previous)<.04,"Opposite key ramps smoothly");previous=next;}
  Check(k.Bias<0&&k.Output<-.4,"Held opposite key adds more countersteer progressively");
  double oldBias=k.Bias;k.Step(.02,true,0,-.4,.833,0,k.Output);Check(Math.Abs(k.Bias)>Math.Abs(oldBias)*.9,"Release retains short steering memory");
  for(int i=0;i<100;i++)k.Step(.02,true,0,.35,.833,0,k.Output);
  Check(Math.Abs(k.Bias)<.001&&Math.Abs(k.Output-.35)<.001,"SC follows new slide naturally after release");
  previous=k.Output;k.Step(.02,false,0,0,.833,.003,previous);Check(k.Blending&&k.Output>.25,"Settling does not snap steering straight");
  for(int i=0;i<100;i++)k.Step(.02,false,0,0,.833,.003,k.Output);
  Check(!k.Blending&&k.Output==.003,"Ordinary turn-in returns after smooth handover");
  k.Step(.02,true,1,-.4,.833,0,0);k.Reset();Check(!k.Blending&&k.Bias==0,"Disable/reset clears input memory");
  foreach(double degrees in new[]{25.0,50.0,75.0})foreach(int sign in new[]{-1,1}){
   double physical=Math.Max(60,degrees+15),scCap=degrees/physical,extra=15/physical;
   var margin=new DriftKeyboardBlend();
   for(int i=0;i<100;i++)margin.Step(.02,true,sign,sign*scCap,scCap,0,sign*scCap,extra);
   Check(Math.Abs(margin.Output*physical-sign*(degrees+15))<.001,"Keys add exactly 15 degrees beyond saturated SC in either direction");
   double peak=margin.Output;margin.Step(.02,true,0,sign*scCap,scCap,0,peak,extra);
   Check(Math.Abs(margin.Output)>scCap&&Math.Abs(margin.Output)<Math.Abs(peak),"Extra steering fades smoothly on release");
   for(int i=0;i<160;i++)margin.Step(.02,true,0,sign*scCap,scCap,0,margin.Output,extra);
   Check(Math.Abs(margin.Output-sign*scCap)<.0001,"Release returns to SC cap without remaining keyboard angle");
   margin.Reset();for(int i=0;i<100;i++)margin.Step(.02,true,0,sign,scCap,0,0,extra);
   Check(Math.Abs(margin.Output)<=scCap+.0001,"SC alone cannot use reserved keyboard angle");
   var r=KDACore.Evaluate(3,1,12,60,1,physical,2.7,9.81,50,.2,0,0,true,false,1,degrees);
   Check(!Double.IsNaN(r.Correction)&&!Double.IsInfinity(r.Correction),"Drift yaw calculation remains finite with 90-degree physical lock");
  }
  Console.WriteLine("PASS: progressive taps/transitions and separate SC/keyboard caps with 15-degree override");
 }
}
