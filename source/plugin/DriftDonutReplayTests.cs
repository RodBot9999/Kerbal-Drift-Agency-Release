using System;
using System.Globalization;
using System.IO;
class DriftDonutReplayTests {
 static void Main(string[] args){
  var lines=File.ReadAllLines(args[0]);var header=lines[0].Split(',');
  var keyboard=new DriftKeyboardBlend();var intent=new DriftSteeringIntent();
  int checks=0,larger=0,eligible=0;double previous=0,max=0;
  using(var output=new StreamWriter(args[1])){
   output.WriteLine("time_s,recorded_bias_deg,new_bias_deg,recorded_output_deg,new_output_deg");
   for(int i=1;i<lines.Length;i++){
    var row=lines[i].Split(',');Func<string,double> n=k=>Double.Parse(row[Array.IndexOf(header,k)],CultureInfo.InvariantCulture);
    if(n("sc_mode")!=3||n("sc_enabled")==0||n("grounded_wheels")<2||n("burnout")!=0){keyboard.Reset();intent.Reset();previous=0;continue;}
    double physical=Math.Max(60,n("drift_lock")+15),cap=n("drift_lock")/physical,extra=15/physical,dt=n("fixed_dt");
    bool active=n("sc_active")==1;
    if(n("forward_mps")<4)intent.Reset();else intent.Step(dt,3,active);
    bool drifting=active||intent.Active;
    double ordinary=Math.Max(-n("recovery_limit"),Math.Min(n("recovery_limit"),n("driver_steer")+n("sc_correction")));
    double value=keyboard.Step(dt,drifting,n("raw_steer"),n("sc_correction"),cap,ordinary,previous,extra,n("speed_kmh")/3.6);
    if(Double.IsNaN(value)||Double.IsInfinity(value)||Math.Abs(value)>1+1e-9)throw new Exception("Unbounded recorded-motion steering replay");checks++;
    if(drifting&&Math.Abs(value)>cap+extra+1e-9)throw new Exception("Exceeded SC plus 15-degree physical cap");checks++;
    if(drifting&&n("grounded_wheels")==4&&n("forward_mps")>=4&&n("raw_steer")*n("sc_correction")<0){
     eligible++;if(Math.Abs(keyboard.Bias*physical)>Math.Abs(n("drift_bias")*physical)+3)larger++;
    }
    max=Math.Max(max,Math.Abs(keyboard.Bias*physical));previous=value;
    output.WriteLine(String.Join(",",new[]{n("time_s"),n("drift_bias")*physical,keyboard.Bias*physical,n("output_steer")*physical,value*physical}.SelectInvariant()));
   }
  }
  if(larger<100)throw new Exception("Replay did not expand sustained driver authority");
  Console.WriteLine("PASS "+checks+" replay checks; "+larger+" of "+eligible+" less-countersteer samples gain >3 degrees; maximum manual trim="+max.ToString("F2")+"deg. Recorded motion only, not closed-loop handling validation.");
 }
}
static class InvariantNumbers {
 public static string[] SelectInvariant(this double[] values){var r=new string[values.Length];for(int i=0;i<r.Length;i++)r[i]=values[i].ToString("R",CultureInfo.InvariantCulture);return r;}
}
