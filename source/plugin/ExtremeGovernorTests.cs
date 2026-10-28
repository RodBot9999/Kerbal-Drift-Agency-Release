// SPDX-License-Identifier: MIT
using System;using System.IO;using System.Linq;using System.Globalization;using KerbalDriftAgency;
class ExtremeGovernorTests {
 static int checks;static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
 static void Main(string[] files){
  foreach(int mode in new[]{0,1,2,3})Check(ExtremeSpeedGovernor.Applies(true,mode,false,1)==(mode==1||mode==2),"Race/Sport scope only");
  Check(!ExtremeSpeedGovernor.Applies(true,2,true,1)&&!ExtremeSpeedGovernor.Applies(true,2,false,0)&&!ExtremeSpeedGovernor.Applies(false,2,false,1),"burnout/off/legacy bypass");
  foreach(double speed in new[]{0.0,25,50,65})Check(ExtremeSpeedGovernor.Command(.7,.01,speed,40,2.6,.02,.3,0,0,1)==.7,"original handling below threshold");
  foreach(double speed in new[]{80.0,90,108,130})foreach(double old in new[]{-.8,0,.8})foreach(double yaw in new[]{-2.0,0,2}){
   double limit=ExtremeSpeedGovernor.Limit(speed,40,2.6,9.81,3);
   double command=ExtremeSpeedGovernor.Command(old,.008,speed,40,2.6,limit,yaw,0,0,1);
   Check(Math.Abs(command)<=1.5*limit+1e-10,"final path cannot bypass the high-speed envelope");
   Check(Math.Abs(command-Math.Min(limit,.008))<=.5*limit+1e-10,"automatic correction bounded separately from driver");
  }
  Check(Math.Abs(ExtremeSpeedGovernor.Command(.8,0,108,40,2.6,.011,0,0,0,1))<1e-10,"no residual keyboard/recovery output at straight-line equilibrium");
  Check(Math.Abs(ExtremeSpeedGovernor.Command(.8,0,108,40,2.6,.011,.2,.4,0,1))<1e-10,"no understeer turn-in boost");
  var wheel=new DrivelineWheelState{Weight=1,Grounded=true,Slip=0,ShaftSpeed=1000,ExpectedShaftSpeed=1000};var zero=wheel;zero.Weight=0;
  var config=new ActiveDrivelineConfig{RearVectoring=true,RearOffset=ExtremeSpeedGovernor.VectorOffset(.35,108),RearRate=3};var drive=new ActiveDrivelineCore();ActiveDriveAllocation allocation=new ActiveDriveAllocation();
  var spinning=wheel;spinning.Slip=1;
  for(int i=0;i<100;i++){allocation=drive.Step(.02,config,zero,zero,wheel,spinning,0,2,true,false);Check(Math.Abs(allocation.FL+allocation.FR+allocation.RL+allocation.RR-1)<1e-10,"total drive allocation conserved");}
  Check(allocation.RearLeftShare>=.47&&allocation.RearLeftShare<=.53,"extreme-speed redistribution stays within three percentage points");
  Check(ExtremeSpeedGovernor.VectorOffset(.35,50)==.35,"normal-speed hardware calibration preserved");
  foreach(string file in files){
   var lines=File.ReadAllLines(file);var header=lines[0].Split(',');int n=0;double peakOld=0,peakNext=0;
   foreach(string line in lines.Skip(1)){
    if(line.Length==0)continue;var row=line.Split(',');Func<string,double> val=k=>Double.Parse(row[Array.IndexOf(header,k)],CultureInfo.InvariantCulture);
    if(val("sc_mode")!=2||val("forward_mps")<80||val("grounded_wheels")<2)continue;
    double command=ExtremeSpeedGovernor.Command(val("output_steer"),val("driver_steer"),val("forward_mps"),40,2.6,val("turn_limit"),val("yaw_rad_s"),val("desired_yaw"),val("filtered_yaw_rate_rad_s2"),val("sc_assistance"));
    Check(Math.Abs(command)<=1.5*val("turn_limit")+1e-9,"recorded final steering envelope");peakOld=Math.Max(peakOld,Math.Abs(val("output_steer"))*40);peakNext=Math.Max(peakNext,Math.Abs(command)*40);n++;
   }
   Check(n>100,"final failed recording covered");Console.WriteLine("Fixed-motion replay: "+n+" samples; peak steering command "+peakOld.ToString("F2")+" -> "+peakNext.ToString("F2")+" deg. Native outcome unverified.");
  }
  Console.WriteLine("PASS "+checks+" final-governor checks");
 }
}
