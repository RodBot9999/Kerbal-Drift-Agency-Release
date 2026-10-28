using System;
using System.IO;
using System.Globalization;
class RaceRecoveryIntentTests {
 static int checks;static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 static double N(string[] r,string[] h,string k){return Double.Parse(r[Array.IndexOf(h,k)],CultureInfo.InvariantCulture);}
 static void Main(string[] args){
  foreach(double sign in new[]{-1.0,1.0}){
   var b=new RaceRecoveryBlend();double output=0;
   for(int i=0;i<100;i++)output=b.Step(.02,true,sign,-sign*.04,sign*.006,output,40,.8,sign*6,-sign*.4,sign*.15);
   Check(b.Bias==0,"held input into slip cannot acquire recovery trim");
   Check(output*sign<0,"held turn cannot cancel automatic recovery through trim");
   Check(!RaceRecoveryBlend.Corrective(sign,sign*6,-sign*.4,0),"slide direction dominates yaw direction");
   Check(RaceRecoveryBlend.Corrective(-sign,sign*6,-sign*.4,0),"opposing key retains corrective authority");
   for(int i=0;i<20;i++)output=b.Step(.02,true,-sign,-sign*.3,0,output,40,.8,sign*15,0,0);
   Check(Math.Abs(b.Bias*40+sign*12)<1e-8,"corrective hold builds full twelve degree trim");
   double prior=b.Bias;output=b.Step(.02,true,0,0,0,output,40,.8,-sign*3,0,0);
   Check(Math.Abs(b.Bias)<Math.Abs(prior)&&Math.Abs(b.Bias)>0,"stale trim decays without discontinuity");
   for(int i=0;i<15;i++)output=b.Step(.02,true,0,0,0,output,40,.8,-sign*3,0,0);
   Check(Math.Abs(b.Bias)*40<.01,"stale trim clears promptly on slide reversal");
   double settled=KDACore.RaceControlYaw(sign*.194,sign*.021,0,-sign*3);
   Check(settled==0,"converging actual yaw stops stale filtered countersteer");
   double reversed=KDACore.RaceControlYaw(sign*.2,-sign*.3,0,-sign*3);
   Check(reversed*sign<0,"reversed yaw never retains old filtered sign");
   var r=KDACore.Evaluate(2,1,8,68,0,40,2.7,9.81,0,sign*.194,0,0,true,false,1,50,0,sign*.021,-sign*3);
   Check(r.Correction==0,"released keys settling yaw receives no recovery kick");
   var unchanged=KDACore.Evaluate(2,1,8,68,0,40,2.7,9.81,0,sign*.194,0,0,true,false);
   Check(r.Limit==unchanged.Limit,"yaw update leaves cornering budget untouched");
   var driftA=KDACore.Evaluate(3,1,12,68,0,65,2.7,9.81,20,sign*.194,0,0,true,false);
   var driftB=KDACore.Evaluate(3,1,12,68,0,65,2.7,9.81,20,sign*.194,0,0,true,false,1,50,0,sign*.021,-sign*3);
   var driftExpected=KDACore.Evaluate(3,1,12,68,0,65,2.7,9.81,20,sign*.021,0,0,true,false);
   Check(driftExpected.Correction==driftB.Correction,"Drift small actual yaw is not extrapolated by Race prediction");
  }
  if(args.Length>0){
   var lines=File.ReadAllLines(args[0]);var h=lines[0].Split(',');var blend=new RaceRecoveryBlend();double output=0;int samples=0;
   foreach(var line in lines){if(line==lines[0]||line.Length==0)continue;var r=line.Split(',');double t=N(r,h,"time_s");
    if(t<18.22||t>19.22)continue;
    double key=N(r,h,"raw_steer"),slip=N(r,h,"slip_deg"),yaw=N(r,h,"yaw_rad_s"),desired=N(r,h,"desired_yaw");
    output=blend.Step(N(r,h,"dt_ut"),N(r,h,"sc_active")==1,key,N(r,h,"sc_correction"),N(r,h,"driver_steer"),output,40,.8,slip,yaw,desired);
    Check(blend.Bias==0,"recorded held-turn regression cannot accumulate trim");samples++;
   }
   Check(samples>40,"recording interval exercised");Console.WriteLine("Recorded held-turn interval: "+samples+" samples, zero deepening trim.");
  }
  Console.WriteLine(checks+" Race intent/yaw checks passed");
 }
}
