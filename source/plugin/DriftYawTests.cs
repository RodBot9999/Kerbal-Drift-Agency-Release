using System;
using System.IO;
using System.Globalization;
class DriftYawTests {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static void Main(string[] paths){
  foreach(double sign in new[]{-1.0,1.0}){
   double steady=KDACore.DriftControlYaw(sign*.5,sign*.5,0,0);
   Check(steady==sign*.5,"steady drift yaw unchanged");
   double growing=KDACore.DriftControlYaw(sign*.5,sign*.6,0,sign*2);
   Check(Math.Abs(growing)>Math.Abs(sign*.6)&&growing*sign>0,"accelerating rotation receives early balancing");
   double converging=KDACore.DriftControlYaw(sign*.5,sign*.2,0,-sign*2);
   Check(Math.Abs(converging)<.2&&converging*sign>=0,"settling rotation eases before filtered state");
   Check(KDACore.DriftControlYaw(sign*.5,sign*.15,0,-sign*20)==0,"prediction cannot command the opposite rotation before crossing");
   Check(KDACore.DriftControlYaw(sign*.4,-sign*.2,0,-sign*2)*sign<0,"new rotation direction beats stale observer sign");
   Check(KDACore.DriftControlYaw(sign*.06,sign*.06,0,sign*100)==sign*.06,"straight yaw noise never gains derivative lead");
   Check(Math.Abs(KDACore.DriftControlYaw(sign*.5,sign*.5,0,sign*100)-sign*.68)<1e-9,"growing prediction bounded at 0.18 rad/s");
   Check(KDACore.DriftControlYaw(sign*.5,sign*.5,sign*.5,sign*100)==sign*.5,"intended yaw tracking remains unchanged");
   Check(KDACore.DriftControlYaw(sign*.3,Double.NaN,0,0)==sign*.3,"missing raw yaw falls back safely");
   Check(KDACore.DriftControlYaw(sign*.3,sign*.2,0,Double.NaN)==sign*.2,"bad derivative uses measured yaw without extrapolation");
   var quiet=KDACore.Evaluate(3,1,12,40,0,65,2.65,9.81,sign*.3,sign*.06,0,0,true,false,1,50,0,sign*.06,sign*100);
   Check(quiet.Correction==0,"actual Drift output remains silent in straight noise");
   var drift=KDACore.Evaluate(3,1,12,40,0,65,2.65,9.81,sign*30,-sign*.5,0,0,true,false,1,50,0,-sign*.5,0);
   Check(drift.Correction*sign<0&&drift.RecoveryLimit*65<=50&&drift.Throttle<=1,"large drift balancing direction and lock retained");
  }
  foreach(string path in paths){
   var lines=File.ReadAllLines(path);var header=lines[0].Split(',');int transitions=0,predicted=0,driftSamples=0;double maxLead=0;
   foreach(string line in lines){if(line==lines[0]||line.Length==0)continue;var row=line.Split(',');Func<string,double> n=k=>Double.Parse(row[Array.IndexOf(header,k)],CultureInfo.InvariantCulture);
    if(n("sc_mode")!=3||n("forward_mps")<4||n("sc_active")==0)continue;
    // Isolate the yaw change using recorded states. Vehicle response and
    // keyboard-bias dynamics are not simulated in this replay.
    double raw=n("yaw_rad_s"),filtered=n("filtered_yaw"),desired=n("desired_yaw"),rate=n("yaw_accel_rad_s2");
    double yaw=KDACore.DriftControlYaw(filtered,raw,desired,rate),error=raw-desired;
    Check(!Double.IsNaN(yaw)&&Math.Abs(yaw-raw)<=.180001,"recorded prediction finite and bounded");
    Check((yaw-desired)*error>=-1e-9,"recorded prediction never invents a reversed error");
    if(error*(filtered-desired)<0){transitions++;Check((yaw-desired)*error>=0,"recorded reversal follows actual error direction");}
    if(Math.Abs(yaw-raw)>.02)predicted++;maxLead=Math.Max(maxLead,Math.Abs(yaw-raw));driftSamples++;
   }
   Check(driftSamples>100&&transitions>0&&predicted>0,"recorded Drift transitions and prediction exercised");
   Console.WriteLine("Drift replay: "+driftSamples+" samples, "+transitions+" stale filtered error signs, "+predicted+" meaningful lead samples, max "+maxLead.ToString("F3")+" rad/s. Not closed-loop physics validation.");
  }
  Console.WriteLine("PASS "+checks+" Drift yaw checks");
 }
}
