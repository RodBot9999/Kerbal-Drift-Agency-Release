using System;
namespace KerbalDriftAgency {
 // Positive-drive AWD turn-in only. No integrator, force injection or coast
 // intervention: the established recovery law owns slides and key release.
 public sealed class AwdCorneringCore {
  public double AddedSteering {get;private set;}
  public double YawDeficit {get;private set;}
  public double RearAssist {get;private set;}
  public double Support {get;private set;}
  int lastMode=-1;double lastSign;
  static double Clamp(double x,double lo,double hi){return Math.Max(lo,Math.Min(hi,x));}
  static bool Finite(double x){return !Double.IsNaN(x)&&!Double.IsInfinity(x);}
  static double Fade(double x,double start,double end){return 1-Clamp((x-start)/(end-start),0,1);}
  public void Reset(){AddedSteering=YawDeficit=RearAssist=Support=0;lastMode=-1;lastSign=0;}
  public double Step(double dt,bool eligible,int mode,double assist,double input,double baseline,double desiredYaw,double yaw,double yawRate,double speed,double wheelbase,double maxAngle,double slip,double slipRate,double allowedSlip,double unloading,double roll,double frontSideRatio,double rearSideRatio,double frontSpin,double rearSpin){
   if(!Finite(dt)||!Finite(assist)||!Finite(input)||!Finite(baseline)||!Finite(desiredYaw)||!Finite(yaw)||!Finite(yawRate)||!Finite(speed)||!Finite(wheelbase)||!Finite(maxAngle)||!Finite(slip)||!Finite(slipRate)||!Finite(allowedSlip)||!Finite(unloading)||!Finite(roll)||!Finite(frontSideRatio)||!Finite(rearSideRatio)||!Finite(frontSpin)||!Finite(rearSpin)){Reset();return Finite(baseline)?baseline:0;}
   if(!eligible||mode<0||mode>2||assist<=0||speed<12||wheelbase<1||maxAngle<=0||Math.Abs(input)<.05||input*baseline<=0||input*desiredYaw>=0){Reset();return baseline;}
   double sign=Math.Sign(input);
   if(lastMode!=mode||lastSign!=sign){Reset();lastMode=mode;lastSign=sign;}
   dt=Clamp(dt,0,.1);assist=Clamp(assist,0,1);
   // All three profiles share the Race law; lower modes have smaller authority,
   // stricter slip/load gates and slower application. They retain fast release.
   double gain=mode==2?1.25:mode==1?.85:.50;
   double fraction=mode==2?.85:mode==1?.55:.30;
   double degrees=mode==2?2.5:mode==1?1.5:.8;
   double slipEnd=Math.Min(Math.Max(.8,allowedSlip),mode==2?5:mode==1?3:1.5);
   double slipStart=slipEnd*.45;
   double growth=Math.Max(0,Math.Sign(slip)*slipRate);
   double predictedSlip=Math.Abs(slip)+Math.Min(3,growth*.12);
   double motion=Fade(predictedSlip,slipStart,slipEnd)*Fade(growth,mode==2?8:mode==1?5:3,mode==2?25:mode==1?18:12);
   double load=Fade(unloading,mode==2?.55:mode==1?.48:.40,mode==2?.85:mode==1?.78:.70)*Fade(Math.Abs(roll),4,9);
   double tires=Fade(frontSideRatio,.80,1.20)*Fade(rearSideRatio,.65,1.05);
   double traction=Fade(frontSpin,.15,.50)*Fade(rearSpin,.10,.40);
   Support=motion*load*tires;
   double turnSign=-sign,requested=Math.Abs(desiredYaw),actual=turnSign*yaw;
   // Lead a rising yaw rate to remove turn-in before a yaw overshoot develops.
   double predicted=Math.Max(actual,actual+turnSign*yawRate*.10);
   YawDeficit=Math.Max(0,requested-Math.Max(0,predicted)-.025);
   if(actual<-.05)Support=0; // Direction changes remain the recovery driver's job.
   double deficit=Clamp(YawDeficit/Math.Max(.1,requested),0,1);
   double extra=YawDeficit*wheelbase/speed*180/Math.PI/maxAngle*gain;
   extra=Math.Min(extra,Math.Min(degrees/maxAngle,Math.Abs(baseline)*fraction));
   double target=sign*extra*Support*traction*assist;
   double tau=Math.Abs(target)<Math.Abs(AddedSteering)?.045:mode==2?.18:mode==1?.23:.30;
   AddedSteering+=(target-AddedSteering)*(1-Math.Exp(-dt/tau));
   // Dynamic caps avoid retaining extra lock as the speed budget/input shrinks.
   AddedSteering=sign*Math.Min(Math.Abs(AddedSteering),Math.Min(degrees/maxAngle,Math.Abs(baseline)*fraction)*Support*traction*assist);
   double rearTraction=Fade(rearSpin,.10,.40);
   double rearTarget=Support*rearTraction*deficit*Clamp((frontSpin-rearSpin-.04)/.20,0,1)*(mode==2?.12:mode==1?.08:.04)*assist;
   RearAssist+=(rearTarget-RearAssist)*(1-Math.Exp(-dt/(rearTarget<RearAssist?.045:.20)));
   RearAssist=Math.Min(RearAssist,Support*rearTraction*(mode==2?.12:mode==1?.08:.04)*assist);
   return Clamp(baseline+AddedSteering,-1,1);
  }
 }
}
