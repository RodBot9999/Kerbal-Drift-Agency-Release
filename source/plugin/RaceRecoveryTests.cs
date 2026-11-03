using System;
using System.IO;
using System.Globalization;
class RaceRecoveryTests {
 static int checks;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 static KDACore.Result Run(double slip,double rate,double yaw=0,int mode=2){return KDACore.Evaluate(mode,1,8,50,0,40,2.7,9.81,slip,yaw,0,0,true,false,1,50,rate);}
 static double Number(string[] row,string[] header,string key){return Double.Parse(row[Array.IndexOf(header,key)],CultureInfo.InvariantCulture);}
 static void Main(string[] args){
  foreach(double sign in new[]{-1.0,1.0}){
   var below=Run(sign*7.999,0);var above=Run(sign*8.001,0);
   Check(Math.Abs(above.Correction-below.Correction)*40<.001,"continuous slip threshold");
   var steady=Run(sign*20,0);var growing=Run(sign*20,sign*60);var recovering=Run(sign*20,-sign*60);
   Check(Math.Abs(recovering.Correction)<Math.Abs(steady.Correction)*.5,"convergence reduces countersteer early");
   Check(growing.Correction==steady.Correction,"prediction never amplifies growing slip");
   Check(Run(sign*10,-sign*100).Correction==0,"no opposite prediction kick before zero crossing");
   Check(steady.RecoveryLimit==.8&&recovering.RecoveryLimit==.8,"Race retains 32 degree cap");
   Check(steady.Limit==recovering.Limit&&steady.Limit==growing.Limit,"cornering budget independent of recovery");
   Check(Run(sign*20,Double.NaN).Correction==steady.Correction,"invalid rate ignored");
   Check(Run(sign*20,0,0,3).Correction==Run(sign*20,-sign*60,0,3).Correction,"Drift correction unchanged by rate");
   var b=new RaceRecoveryBlend();double first=b.Step(.02,true,sign,sign*.4,0,sign*.4,40,.8,-sign*20,0,0);
   Check(Math.Abs(first-sign*.4)*40<=1.21,"keyboard entry has no full lock step");
   for(int i=0;i<20;i++)b.Step(.02,true,sign,sign*.4,0,first,40,.8,-sign*20,0,0);
   Check(Math.Abs(b.Bias*40-12*sign)<1e-8,"held keyboard trim bounded at twelve degrees");
   double last=b.Bias; b.Step(.02,true,-sign,sign*.4,0,first,40,.8,sign*20,0,0);
   Check(Math.Abs(b.Bias)<Math.Abs(last),"old trim withdraws at slide reversal");
   for(int i=0;i<100;i++)b.Step(.02,false,0,0,0,0,40,.8,0,0,0);
   Check(!b.Blending&&b.Bias==0,"handover returns to ordinary steering");
   b.Step(.02,true,sign,sign*.8,0,0,40,.8,-sign*20,0,0);b.Reset();
   Check(!b.Blending&&b.Bias==0,"airborne disabled reset clears recovery state");
  }
  var observer=new StabilityObserver();for(int i=0;i<100;i++)observer.Step(.02,20,0,0,8);
  for(int i=0;i<10;i++)observer.Step(.02,20-i*.6,0,0,8);
  Check(observer.SlipRate<0,"observer detects convergence");observer.Reset();Check(observer.SlipRate==0,"observer reset clears derivative");
  var entry=new RaceRecoveryBlend();double entered=entry.Step(.02,true,0,0,.1,.1,40,.8,0,0,0);
  Check(Math.Abs(entered-.1)<1e-9,"ordinary demand not counted twice on activation");
  foreach(string path in args){
   string[] lines=File.ReadAllLines(path),header=lines[0].Split(',');var o=new StabilityObserver();
   double largest=0;int grounded=0;
   foreach(string line in lines){if(line==lines[0]||line.Length==0)continue;var row=line.Split(',');
    double speed=Number(row,header,"forward_mps"),ground=Number(row,header,"grounded_wheels");
    if(speed<4||ground<2){o.Reset();continue;}
    double tolerance=Number(row,header,"allowed_oversteer")+3;
    o.Step(Number(row,header,"dt_ut"),Number(row,header,"slip_deg"),Number(row,header,"yaw_rad_s"),Number(row,header,"desired_yaw"),tolerance);
    // Recorded states are replayed, not a closed-loop simulation of the new car.
    double t=Number(row,header,"time_s");var result=Run(o.Slip,o.SlipRate,o.Yaw);
    Check(!Double.IsNaN(result.Correction)&&Math.Abs(result.Correction)<=.8,"replayed correction finite and bounded");
    if(ground==4){grounded++;largest=Math.Max(largest,Math.Abs(result.Correction)*40);}
    if(path.Contains("223901")&&Math.Abs(t-33.82)<.005){
     double old=Number(row,header,"sc_correction");
     Check(Math.Abs(result.Correction)<Math.Abs(old),"measured recovery receives less lingering countersteer");
     Console.WriteLine("33.82s recorded correction="+(old*40).ToString("F2")+"deg replay="+(result.Correction*40).ToString("F2")+"deg filtered rate="+o.SlipRate.ToString("F2"));
    }
   }
   Console.WriteLine(Path.GetFileName(Path.GetDirectoryName(path))+": grounded samples="+grounded+", max replay correction="+largest.ToString("F2")+"deg");
  }
  Console.WriteLine(checks+" Race recovery checks passed");
 }
}
