using System;
using System.IO;
using System.Globalization;
class RaceCorneringTests {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static KDACore.Result Run(double slip,double rate,double yaw=0,double input=0,int mode=2){return KDACore.Evaluate(mode,1,5,85,input,40,2.65,9.81,slip,yaw,0,0,true,false,3,50,rate,yaw,0);}
 static void Main(string[] paths){
  foreach(double sign in new[]{-1.0,1.0}){
   var guard=new RaceCorneringGuard();
   for(int i=0;i<200;i++)guard.Step(.02,true,sign,sign*.3,0,5);
   Check(guard.Scale==1,"stable held turn retains full existing budget");
   double prior=guard.Scale;
   for(int i=0;i<20;i++){double scale=guard.Step(.02,true,sign,sign*3,sign*15,5);Check(scale<=prior&&scale>=.1,"growing slip withdraws turn-in smoothly");prior=scale;}
   Check(guard.Scale<.3,"guard responds before the old eight degree threshold");
   Check(guard.Step(.02,true,-sign,sign*15,sign*30,5)==1,"manual countersteering is never scaled down");
   Check(guard.Step(.02,true,0,sign*15,sign*30,5)==1,"release clears turn guard for neutral settling");
   guard.Step(.02,true,sign,sign*8,sign*30,5);
   Check(guard.Step(.02,false,sign,sign*8,sign*30,5)==1,"disabled/other mode resets guard");
   Check(guard.Step(.02,true,sign,Double.NaN,0,5)==1,"invalid motion resets guard");
   var steady=Run(sign*4,0);var growth=Run(sign*4,sign*20);
   Check(steady.Correction==0&&growth.Correction*sign<0&&Math.Abs(growth.Correction)*40>2,"incipient slide produces meaningful predictive steering");
   Check(growth.RecoveryLimit==.8,"prediction can use full recovery authority before measured threshold");
   Check(growth.Limit==steady.Limit,"static cornering budget unchanged");
   var converging=Run(sign*8,-sign*100);
   Check(converging.Correction==0,"converging slip never predicts an opposite kick");
   Check(Run(sign*.3,sign*2,sign*.07).Correction==0,"straight motion noise remains quiet");
   Check(Run(sign*4,sign*20,0,0,3).Correction==Run(sign*4,0,0,0,3).Correction,"Drift ignores growing-slip lead");
   Check(Run(sign*4,sign*20,0,0,1).Correction==Run(sign*4,0,0,0,1).Correction,"Sport unchanged");
   // Lead fades continuously as recovery becomes a large-slide problem.
   Check(Math.Abs(Run(sign*8.999,sign*30).Correction-Run(sign*9.001,sign*30).Correction)*40<.01,"no correction step at the lead fade boundary");
  }
  foreach(string path in paths){
   var lines=File.ReadAllLines(path);var h=lines[0].Split(',');var guard=new RaceCorneringGuard();var observer=new StabilityObserver();var settling=new RaceNeutralSettling();var blend=new RaceRecoveryBlend();double driver=0,output=0;int reduced=0,early=0;double firstOld=-1,firstNew=-1;
   foreach(var line in lines){if(line==lines[0]||line.Length==0)continue;var row=line.Split(',');Func<string,double> n=k=>Double.Parse(row[Array.IndexOf(h,k)],CultureInfo.InvariantCulture);
    double speed=n("forward_mps"),dt=n("fixed_dt"),raw=n("raw_steer"),slip=n("slip_deg"),yaw=n("yaw_rad_s"),allowed=n("allowed_oversteer"),t=n("time_s");
    bool eligible=n("sc_mode")==2&&n("sc_enabled")==1&&n("grounded_wheels")>=2&&speed>=12&&n("burnout")==0;
    if(!eligible){guard.Reset();observer.Reset();settling.Reset();blend.Reset();driver=output=0;continue;}
    double weight=settling.Step(dt,true,raw,slip,yaw,observer.Active),tolerance=allowed+(Math.Min(allowed,1.5)-allowed)*weight;
    double scale=guard.Step(dt,true,raw,slip,observer.SlipRate,allowed),demand=Math.Max(-n("turn_limit"),Math.Min(n("turn_limit"),raw))*scale;
    driver+=(demand-driver)*(1-Math.Exp(-dt/KDACore.SteeringTau(driver,demand,.16,2)));
    double desired=-driver*Math.Tan(40*Math.PI/180)*speed/2.65;
    observer.Step(dt,slip,yaw,desired,tolerance);
    var r=KDACore.Evaluate(2,1,tolerance,speed,driver,40,2.65,9.81,observer.Slip,observer.Yaw,n("roll_ground_deg"),0,true,false,n("cornering_budget_g")/.95,50,observer.SlipRate,yaw,observer.YawRate,weight);
    double correction=observer.Active?r.Correction:0;
    output=blend.Step(dt,observer.Active,raw,correction,driver,output,40,Math.Max(n("turn_limit"),.8),slip,yaw,desired);
    Check(!Double.IsNaN(output)&&Math.Abs(output)<=1,"replay finite and bounded");
    if(raw*slip<0)Check(scale==1,"replay preserves every manual countersteering input");
    if(Math.Abs(raw)>.02&&scale<.8)reduced++;
    if(Math.Abs(raw)>.02&&Math.Abs(slip)<8&&Math.Abs(correction)>Math.Abs(n("sc_correction"))+.02)early++;
    if(path.Contains("1dcfab")&&t>=19.62&&t<=21.12){
     if(firstOld<0&&Math.Abs(n("sc_correction"))*40>=2)firstOld=t;
     if(firstNew<0&&Math.Abs(correction)*40>=2)firstNew=t;
     if(Math.Abs(t-20.6)<.005)Console.WriteLine("20.60s recorded correction="+(n("sc_correction")*40).ToString("F2")+"deg replay="+(correction*40).ToString("F2")+"deg, turn-in="+scale.ToString("F2"));
    }
   }
   Check(reduced>0&&early>0,"replay intervenes earlier and guards held turn-in");
   if(path.Contains("1dcfab")){Check(firstNew>0&&firstOld>firstNew,"full-spin held-turn correction begins earlier");Console.WriteLine("Held turn reaches 2deg correction: recorded="+firstOld.ToString("F2")+"s replay="+firstNew.ToString("F2")+"s");}
   Console.WriteLine(Path.GetFileName(Path.GetDirectoryName(path))+": "+reduced+" guarded held samples; "+early+" stronger early correction samples");
  }
  Console.WriteLine("PASS "+checks+" Race cornering checks. Recorded-motion replay is not closed-loop physics validation.");
 }
}
