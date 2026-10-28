using System;
using System.Collections.Generic;
using KerbalDriftAgency;

// Source full-load crank rating only. The imported base torque has already
// had boost removed; restore its combined exported pressure exactly once.
// This does not run, mutate or retune the live engine.
public static class GaragePowerRating {
 public struct Result {public double TorqueNm,PowerKw;}
 public static Result AtRpm(EngineCore source,double rpm){
   if(source==null||rpm<0||rpm>source.LimitRpm)return new Result();
   double boost;
   if(source.ImportedBoostMap&&source.BoostRpm.Length>1)boost=source.EvaluateBoost(rpm)*source.MaxBoost/Math.Max(.001,source.ImportedPeakBoost);
   else{
    double spool=EngineCore.Clamp((rpm-source.BoostStartRpm)/Math.Max(100,source.BoostFullRpm-source.BoostStartRpm),0,1);
    spool=Math.Pow(spool*spool*(3-2*spool),EngineCore.Clamp(source.SpoolExponent*source.ThresholdShape,1,3));
    double taper=1-(1-EngineCore.Clamp(source.HighRpmBoostFraction,0,1))*EngineCore.Clamp((rpm-source.FlowTaperStartRpm)/Math.Max(100,source.FlowTaperEndRpm-source.FlowTaperStartRpm),0,1);
    boost=source.MaxBoost*taper*spool;
   }
   double torque=source.EvaluateTorque(rpm)*(1+Math.Max(0,boost)*EngineCore.Clamp(source.BoostTorqueGain,0,2))*EngineCore.Clamp(source.ParasiticFactor,.5,1);
   return new Result{TorqueNm=torque,PowerKw=torque*rpm*Math.PI/30000};
 }
 public static Result Calculate(EngineCore source){
  var result=new Result();var points=new SortedSet<double>();foreach(double rpm in source.CurveRpm)points.Add(rpm);if(source.ImportedBoostMap)foreach(double rpm in source.BoostRpm)points.Add(rpm);
  foreach(double rpm in points){
   if(rpm<0||rpm>source.LimitRpm)continue;
   var point=AtRpm(source,rpm);result.TorqueNm=Math.Max(result.TorqueNm,point.TorqueNm);result.PowerKw=Math.Max(result.PowerKw,point.PowerKw);
  }
  return result;
 }
}
