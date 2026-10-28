using System;

// Small chassis aid: no extra tire forces and no artificial body rotation.
public static class KDACore {
 public struct Setup {public double FrontSpring,RearSpring,Height,FrontBar,RearBar,Grip,Bump,Rebound,FastBump,FastRebound;}
 public struct Result {public double Steering,Throttle,Correction,Limit,RecoveryLimit;}
 static double Clamp(double v,double a,double b){return Math.Max(a,Math.Min(b,v));}
 static bool Finite(double v){return !Double.IsNaN(v)&&!Double.IsInfinity(v);}
 public static Setup Stiffen(Setup s,double factor){
  s.FrontSpring*=factor;s.RearSpring*=factor;s.FrontBar*=factor;s.RearBar*=factor;
  // Damper coefficient = critical damping * ratio. Critical damping already
  // rises with sqrt(spring); this ratio change makes total damping scale with k.
  double damping=Math.Sqrt(factor);
  s.Bump*=damping;s.Rebound*=damping;s.FastBump*=damping;s.FastRebound*=damping;return s;
 }
 public static Setup Preset(int mode){
  if(mode==3)return new Setup{FrontSpring=190,RearSpring=155,Height=-8,FrontBar=45,RearBar=30,Grip=1.15,Bump=.85,Rebound=1.30,FastBump=.55,FastRebound=.85};
  if(mode==2)return Stiffen(new Setup{FrontSpring=202.5,RearSpring=180,Height=-12,FrontBar=63,RearBar=42,Grip=2,Bump=.975,Rebound=1.425,FastBump=.60,FastRebound=.975},1.30);
  if(mode==1)return Stiffen(new Setup{FrontSpring=135,RearSpring=120,Height=0,FrontBar=39,RearBar=27,Grip=1.15,Bump=.75,Rebound=1.20,FastBump=.525,FastRebound=.825},1.25);
  return Stiffen(new Setup{FrontSpring=90,RearSpring=82.5,Height=14,FrontBar=24,RearBar=15,Grip=.75,Bump=.60,Rebound=.975,FastBump=.375,FastRebound=.675},1.25);
 }
 // Preserve high-speed turn-in limits, but allow a driver to use recovery
 // lock when their input opposes a confirmed slide or excess yaw.
 public static double DriverLimit(double input,double slip,double yaw,bool active,Result r){
  return active&&(input*slip<0||(Math.Abs(slip)<1&&input*yaw>0))?Math.Max(r.RecoveryLimit,r.Limit):r.Limit;
 }
 public static double SteeringTau(double current,double demand,double smoothing,int mode){
  if(current*demand<0)return mode==3?.035:.045;
  if(Math.Abs(demand)<Math.Abs(current))return .06;
  return mode>=2?Math.Min(mode==3?.09:.10,Clamp(smoothing,.05,.35)):Clamp(smoothing,.05,.35);
 }
 public static double ImportedCapacity(double frontPeak,double downforce,double mass,double gravity){
  if(!Finite(frontPeak)||!Finite(downforce)||!Finite(mass)||!Finite(gravity))return 1;
  double tire=Clamp(frontPeak/1.15,.65,2.5);
  double aero=1+Clamp(downforce/Math.Max(1,mass*gravity),0,1.5);
  return Clamp(tire*aero,.65,3);
 }
 public static double DriftLock(double degrees){return Finite(degrees)?Clamp(degrees,25,75):50;}
 public static double RecoveryLock(int mode,double maxAngle,double strength,double driftLockDegrees=50){return Clamp((mode==3?DriftLock(driftLockDegrees):mode==2?32:25)/Math.Max(.001,maxAngle)*Clamp(strength,0,1),0,1);}
 public static double CorneringScale(bool imported,int mode,double capacity,double adjustmentPercent){
  if(!Finite(capacity))capacity=1;if(!Finite(adjustmentPercent))adjustmentPercent=0;
  double preset=imported?(mode==2?1.25:mode==1?1.10:1):1;
  return Clamp(capacity*preset*(1+Clamp(adjustmentPercent,-40,60)/100),.3,6);
 }
 public static Result Evaluate(int mode,double strength,double allowedSlip,double speed,double steer,double maxAngle,double wheelbase,double gravity,double slipDegrees,double yaw,double rollDegrees,double unloading,bool grounded,bool intentionalSlip,double capacityScale=1,double driftLockDegrees=50,double slipRate=0,double currentYaw=Double.NaN,double yawRate=0,double neutralSettling=0){
  var r=new Result{Steering=Finite(steer)?Clamp(steer,-1,1):0,Throttle=1,Limit=1,RecoveryLimit=1};
  if(!Finite(speed)||!Finite(maxAngle)||!Finite(wheelbase)||!Finite(gravity)||!Finite(slipDegrees)||!Finite(yaw)||!Finite(rollDegrees)||!Finite(unloading)||!Finite(strength)||!Finite(allowedSlip)||!Finite(steer))return r;
  strength=Clamp(strength,0,1);if(!grounded||speed<4||strength==0)return r;
  // Forward-only correction; reversing retains normal low-speed wheel control.
  double angle=Math.Max(.001,maxAngle*Math.PI/180),g=Math.Max(.1,gravity);
  double lateralG=(mode>=2?.95:mode==1?.70:.48)*(Finite(capacityScale)?Clamp(capacityScale,.3,6):1);
  double safeAngle=Math.Atan(lateralG*g*Math.Max(1,wheelbase)/(speed*speed));
  double danger=Math.Max(Clamp((Math.Abs(rollDegrees)-5)/14,0,1),Clamp((unloading-.35)/.55,0,1));
  double limit=Clamp(safeAngle/angle,.003,1)*(1-.65*danger);
  r.Limit=1-strength*(1-limit);
  r.RecoveryLimit=r.Limit;
  double requested=Clamp(steer,-r.Limit,r.Limit);
  if(!intentionalSlip){
   double excess=Math.Max(0,Math.Abs(slipDegrees)-Math.Max(0,allowedSlip));
   // Once outside the deadband, align the tires with the actual slide angle.
   // Subtracting the entire tolerated angle used to leave only a few degrees
   // of recovery lock available during a fast, sustained drift.
   double slipError=(excess>0?slipDegrees:0)*Math.PI/180;
   if(mode==2){
    // Predict convergence through the observer/servo delay. Do not command
    // the opposite slide before measured slip has actually changed sign.
    double rate=Finite(slipRate)?Clamp(slipRate,-180,180):0;
    double settle=Finite(neutralSettling)?Clamp(neutralSettling,0,1):0;
    double predicted=slipDegrees+(rate*slipDegrees<0?rate*(.18-.08*settle):0);
    // Anticipate the beginning of a slide, rather than waiting for the
    // filtered body angle to become a drift. Bound the lead and stop adding
    // it once recovery is already handling a large slide.
    if(rate*slipDegrees>0)
     predicted+=Math.Sign(slipDegrees)*Math.Min(3,Math.Abs(rate)*.10)*Clamp((Math.Max(0,allowedSlip)+4-Math.Abs(slipDegrees))/3,0,1);
    if(predicted*slipDegrees<0)predicted=0;
    double ramp=Clamp(Math.Max(0,Math.Abs(predicted)-Math.Max(0,allowedSlip))/1.5,0,1);
    ramp=ramp*ramp*(3-2*ramp);
    slipError=predicted*Math.PI/180*ramp;
   }
   double desiredYaw=-(mode==3?Math.Tan(requested*angle):requested*Math.Tan(angle))*speed/Math.Max(1,wheelbase);
   double controlYaw=Finite(currentYaw)?(mode==2?RaceControlYaw(yaw,currentYaw,desiredYaw,yawRate):mode==3?DriftControlYaw(yaw,currentYaw,desiredYaw,yawRate):yaw):yaw;
   // Positive wheelSteer turns left on the corrected wheel rig.
   // Do not steer harder simply because the car has not yet reached a
   // theoretical cornering rate. Correct excessive/opposite yaw only.
   double settling=mode==2&&Finite(neutralSettling)?Clamp(neutralSettling,0,1):0;
   double yawThreshold=.10-.06*settling;
   bool yawUnstable=Math.Abs(controlYaw)>Math.Abs(desiredYaw)+yawThreshold||(controlYaw*desiredYaw<0&&Math.Abs(controlYaw)>yawThreshold);
   double yawError=yawUnstable?controlYaw-desiredYaw:0;
   yawError=Math.Sign(yawError)*Math.Max(0,Math.Abs(yawError)-(mode==2?.035-.01*settling:.08));
   double correctionCap=mode==3?RecoveryLock(mode,maxAngle,1,driftLockDegrees):.8;
   double correction=Clamp(-slipError*(mode==3?1.25:.9)/angle+yawError*(mode==3?.20:mode==2?.18+.02*settling:.14)/angle,-correctionCap,correctionCap)*strength;
   double yawExcess=Math.Max(0,Math.Abs(yaw)-Math.Abs(desiredYaw)-.15);
   double recoveryDegrees=(excess>0||yawExcess>0||mode==2&&Math.Abs(slipError)>1e-6)?(mode==3?DriftLock(driftLockDegrees):mode==2?32:25):0;
   // Recovery authority is independent of speed and tire unloading. Those
   // still restrict turn-in; restricting corrective lock can prolong the roll.
   r.RecoveryLimit=Math.Max(r.Limit,Clamp(recoveryDegrees*Math.PI/180/angle*strength,0,1));
   r.Correction=correction;r.Steering=Clamp(requested+correction,-r.RecoveryLimit,r.RecoveryLimit);
   r.Throttle=1-strength*Math.Max(danger,Clamp(excess/8,0,1));
  }else r.Steering=requested;
  return r;
 }
 public static double RaceControlYaw(double filtered,double current,double desired,double rate){
  if(!Finite(current)||!Finite(filtered)||!Finite(desired))return filtered;
  double error=current-desired,old=filtered-desired;
  // A filtered old sign must never continue correcting a settled/reversed yaw.
  if(error*old>0)error=Math.Sign(error)*Math.Min(Math.Abs(error),Math.Abs(old));
  if(Finite(rate)&&error*rate<0){
   double predicted=error+Clamp(rate,-20,20)*.06;
   error=predicted*error>0?predicted:0;
  }
  return desired+error;
 }
 public static double DriftControlYaw(double filtered,double current,double desired,double rate){
  if(!Finite(current)||!Finite(desired))return filtered;
  double error=current-desired;
  // Below the yaw noise gate, never amplify a derivative. During a slide,
  // use actual rotation so a filtered old sign cannot fight a transition.
  if(Math.Abs(error)<=.10||!Finite(rate))return current;
  double lead=Clamp(rate*.05,-.18,.18);
  if(error*rate>0)lead*=Clamp((Math.Abs(error)-.10)/.25,0,1);
  double predicted=error+lead;
  if(predicted*error<0)predicted=0;
  return desired+predicted;
 }
}

public static class ExtremeSpeedGovernor {
 static double Clamp(double v,double lo,double hi){return Math.Max(lo,Math.Min(hi,v));}
 public static bool Applies(bool imported,int mode,bool intentional,double assist){return imported&&(mode==1||mode==2)&&!intentional&&assist>0;}
 public static double Weight(double speed){if(Double.IsNaN(speed)||Double.IsInfinity(speed))return 0;double x=Clamp((speed-65)/15,0,1);return x*x*(3-2*x);}
 public static double VectorOffset(double source,double speed){return source+(Math.Min(source,.03)-source)*Weight(speed);}
 public static double Limit(double speed,double maxAngle,double wheelbase,double gravity,double budget){
  if(Double.IsNaN(speed+maxAngle+wheelbase+gravity+budget)||Double.IsInfinity(speed+maxAngle+wheelbase+gravity+budget))return 0;
  return Clamp(Math.Atan(Math.Max(.15,budget)*Math.Max(.1,gravity)*Math.Max(1,wheelbase)/Math.Max(1,speed*speed))/(Math.Max(1,maxAngle)*Math.PI/180),.003,1);
 }
 public static double Command(double previousPath,double driver,double speed,double maxAngle,double wheelbase,double turnLimit,double yaw,double desired,double yawRate,double assist){
  double weight=Weight(speed);if(weight<=0)return previousPath;
  if(Double.IsNaN(driver+maxAngle+wheelbase+turnLimit+yaw+desired+assist)||Double.IsInfinity(driver+maxAngle+wheelbase+turnLimit+yaw+desired+assist))return 0;
  double limit=Clamp(turnLimit,.003,1),error=yaw-desired;
  bool excess=Math.Abs(yaw)>Math.Abs(desired)+.08||yaw*desired<0&&Math.Abs(yaw)>.08;
  if(!excess)error=0;
  if(!Double.IsNaN(yawRate)&&!Double.IsInfinity(yawRate)&&error*yawRate<0){double next=error+Clamp(yawRate,-20,20)*.08;error=next*error>0?next:0;}
  double correction=error*Math.Min(.06,.35*Math.Max(1,wheelbase)/Math.Max(1,speed))/(Math.Max(1,maxAngle)*Math.PI/180)*Clamp(assist,0,1);
  correction=Clamp(correction,-.5*limit,.5*limit);
  double target=Clamp(Clamp(driver,-limit,limit)+correction,-1.5*limit,1.5*limit);
  return previousPath+(target-previousPath)*weight;
 }
}

// Intent is separate from the slide observer: a drift transition briefly
// crosses zero slip without becoming an ordinary high-speed corner.
public sealed class DriftSteeringIntent {
 double remaining;
 public bool Active {get{return remaining>0;}}
 public void Reset(){remaining=0;}
 public void Step(double dt,int mode,bool sliding){
  if(mode!=3){Reset();return;}
  remaining=sliding?.45:Math.Max(0,remaining-Math.Max(0,Math.Min(.1,dt)));
 }
 public double Limit(int mode,double ordinary,double recovery){return mode==3&&Active?Math.Max(ordinary,recovery):ordinary;}
}

// A time-based steering trim around the aid's balancing angle. Duty cycle,
// rather than key-down edges, determines how repeated taps accumulate.
public sealed class DriftKeyboardBlend {
 public double Bias {get;private set;}
 public double Output {get;private set;}
 public bool Blending {get;private set;}
 double balance;
 static double Approach(double current,double target,double dt,double seconds){return current+(target-current)*(1-Math.Exp(-dt/seconds));}
 static double Clamp(double x,double cap){return Math.Max(-cap,Math.Min(cap,x));}
 public void Reset(){Bias=Output=balance=0;Blending=false;}
 public static double BiasLimit(double cap,double keyboardExtra,double speed){
  // The extra physical lock is separate from how much of SC's balancing
  // angle the driver may trim. Donuts need more range, not faster key taps.
  double slow=Double.IsNaN(speed)||Double.IsInfinity(speed)?0:1-Math.Max(0,Math.Min(1,(Math.Abs(speed)-8)/17));
  return Math.Max(keyboardExtra,cap*(.35+.20*slow));
 }
 public double Step(double dt,bool drifting,double input,double correction,double cap,double ordinary,double previous,double keyboardExtra=0,double speed=25){
  dt=Math.Max(0,Math.Min(.1,dt));cap=Math.Max(0,Math.Min(1,cap));input=Clamp(input,1);
  keyboardExtra=Math.Max(0,Math.Min(1,keyboardExtra));
  double biasCap=BiasLimit(cap,keyboardExtra,speed);
  double releaseSeconds=keyboardExtra>0?.32:.26;
  double totalCap=Math.Min(1,cap+keyboardExtra);
  if(drifting){
   if(!Blending){balance=Clamp(previous,cap);Bias=0;Blending=true;}
   balance=Approach(balance,Clamp(correction,cap),dt,.08);
   if(Math.Abs(input)>.02){
    double rate=input*Bias<0?1.4:.9;
    Bias=Clamp(Bias+input*cap*rate*dt,biasCap);
   }else Bias=Approach(Bias,0,dt,releaseSeconds);
   Bias=Clamp(Bias,biasCap);
   // No hidden trim beyond physical lock: reversing/releasing the key
   // must not first unwind a saturated command the wheels never received.
   Bias=Math.Max(-totalCap-balance,Math.Min(totalCap-balance,Bias));
   Output=Clamp(balance+Bias,totalCap);
  }else if(Blending){
   Bias=Approach(Bias,0,dt,releaseSeconds);
   Output=Approach(Output,ordinary,dt,.12);
   balance=Output-Bias;
   if(Math.Abs(Output-ordinary)<.002&&Math.Abs(Bias)<.002){Reset();Output=ordinary;}
  }else Output=ordinary;
  return Output;
 }
}

// Evidence gate for recovery steering, independent of normal keyboard ramps.
public sealed class StabilityObserver {
 public double Slip,Yaw;
 public double SlipRate {get;private set;}
 public double YawRate {get;private set;}
 public bool Active;
 double evidence,quiet,previousRawSlip,previousRawYaw;bool initialized;
 public void Reset(){Slip=Yaw=evidence=quiet=SlipRate=YawRate=previousRawSlip=previousRawYaw=0;Active=initialized=false;}
 public void Step(double dt,double slip,double yaw,double desiredYaw,double tolerance){
  if(Double.IsNaN(slip)||Double.IsInfinity(slip)||Double.IsNaN(yaw)||Double.IsInfinity(yaw)){Reset();return;}
  dt=Math.Max(0,Math.Min(.1,dt));
  if(!initialized){initialized=true;Slip=Yaw=0;previousRawSlip=slip;previousRawYaw=yaw;}
  double blend=1-Math.Exp(-dt/.08);Slip+=(slip-Slip)*blend;Yaw+=(yaw-Yaw)*blend;
  double delta=slip-previousRawSlip;while(delta>180)delta-=360;while(delta< -180)delta+=360;
  if(dt>0)SlipRate+=(Math.Max(-180,Math.Min(180,delta/dt))-SlipRate)*(1-Math.Exp(-dt/.04));
  previousRawSlip=slip;
  if(dt>0)YawRate+=(Math.Max(-20,Math.Min(20,(yaw-previousRawYaw)/dt))-YawRate)*(1-Math.Exp(-dt/.04));
  previousRawYaw=yaw;
  tolerance=Math.Max(1,tolerance);
  double yawExcess=Math.Abs(Yaw)-Math.Abs(desiredYaw);
  bool opposite=Yaw*desiredYaw<0&&Math.Abs(Yaw)>.10;
  bool unstable=Math.Abs(Slip)>tolerance+.7||yawExcess>.10||opposite;
  bool urgent=Math.Abs(Slip)>tolerance+4||yawExcess>.35;
  if(!Active){evidence=unstable?evidence+dt:0;if(urgent||evidence>=.10){Active=true;quiet=0;}}
  else {
   bool settled=Math.Abs(Slip)<Math.Max(.5,tolerance-.5)&&yawExcess<.04&&!opposite;
   quiet=settled?quiet+dt:0;if(quiet>=.20){Active=false;evidence=quiet=0;}
  }
 }
}

// A released keyboard means settle remaining motion, not preserve a powered
// drift. Engage only on evidence, after a short pause; no heading target or
// integrator. Driver taps and held keys quickly take back ordinary control.
public sealed class RaceNeutralSettling {
 public double Weight {get;private set;}
 double released,quiet;bool engaged;
 public void Reset(){Weight=released=quiet=0;engaged=false;}
 public double Step(double dt,bool eligible,double input,double slip,double yaw,bool recovery){
  if(!eligible||Double.IsNaN(slip)||Double.IsInfinity(slip)||Double.IsNaN(yaw)||Double.IsInfinity(yaw)){Reset();return 0;}
  dt=Math.Max(0,Math.Min(.1,dt));
  if(Math.Abs(input)>.02){released=quiet=0;engaged=false;Weight*=Math.Exp(-dt/.035);return Weight;}
  released+=dt;
  if(released>=.18&&(recovery||Math.Abs(slip)>3||Math.Abs(yaw)>.18))engaged=true;
  bool settled=Math.Abs(slip)<1&&Math.Abs(yaw)<.035;
  quiet=settled?quiet+dt:0;
  if(quiet>=.25)engaged=false;
  Weight+=((engaged?1:0)-Weight)*(1-Math.Exp(-dt/(engaged?.28:.12)));
  return Weight;
 }
}

// A held key requests the strongest stable turn, not an ever-growing slide.
// Only withdraw additional turn-in that deepens measured rear/body slip;
// driver countersteering and the automatic recovery angle are unaffected.
public sealed class RaceCorneringGuard {
 public double Scale {get;private set;}
 public RaceCorneringGuard(){Reset();}
 public void Reset(){Scale=1;}
 public double Step(double dt,bool eligible,double input,double slip,double rate,double allowed){
  if(!eligible||Double.IsNaN(slip)||Double.IsInfinity(slip)||Double.IsNaN(input)||Double.IsInfinity(input)||Double.IsNaN(allowed)||Double.IsInfinity(allowed)||Math.Abs(input)<.02||input*slip<=0){Reset();return Scale;}
  dt=Math.Max(0,Math.Min(.1,dt));allowed=Math.Max(0,Math.Min(18,allowed));
  double growth=Double.IsNaN(rate)||Double.IsInfinity(rate)?0:Math.Max(0,Math.Sign(slip)*rate);
  double predicted=Math.Abs(slip)+Math.Min(3,growth*.12);
  double risk=Math.Max(0,Math.Min(1,(predicted-Math.Max(.75,allowed*.5))/Math.Max(1.5,allowed*.6)));
  risk=risk*risk*(3-2*risk);
  double target=1-.9*risk;
  Scale+=(target-Scale)*(1-Math.Exp(-dt/(target<Scale?.05:.25)));
  return Scale;
 }
}

// Race keys adjust recovery gradually instead of adding a full 32-degree
// driver step to the automatic balance. Turn-in remains in its own budget.
public sealed class RaceRecoveryBlend {
 public double Bias {get;private set;}
 public bool Blending {get;private set;}
 double balance,output;
 static double Clamp(double v,double cap){return Math.Max(-cap,Math.Min(cap,v));}
 static double Approach(double v,double target,double dt,double tau){return v+(target-v)*(1-Math.Exp(-dt/tau));}
 public void Reset(){Bias=balance=output=0;Blending=false;}
 public static bool Corrective(double input,double slip,double yaw,double desired){
  if(Double.IsNaN(slip)||Double.IsInfinity(slip)||Double.IsNaN(yaw)||Double.IsInfinity(yaw))return false;
  if(Math.Abs(slip)>=1.5)return input*slip<0;
  double error=yaw-desired;return Math.Abs(error)>.10&&input*error>0;
 }
 public double Step(double dt,bool active,double input,double correction,double ordinary,double previous,double maxAngle,double cap,double slip,double yaw,double desired){
  dt=Math.Max(0,Math.Min(.1,dt));input=Clamp(input,1);maxAngle=Math.Max(1,maxAngle);cap=Math.Max(0,Math.Min(1,cap));
  if(active){
   if(!Blending){balance=previous-ordinary;Bias=0;Blending=true;}
   bool unwind=Math.Abs(correction)<Math.Abs(balance)||balance*correction<0;
   balance=Approach(balance,correction,dt,unwind?.015:.03);
   bool corrective=Corrective(input,slip,yaw,desired);
   // Withdraw an old trim smoothly when it would now deepen the slide.
   bool stale=Math.Abs(Bias)>.0001&&!Corrective(Bias,slip,yaw,desired);
   if(stale)Bias=Approach(Bias,0,dt,.04);
   if(Math.Abs(input)>.02&&corrective){
    double rate=input*Bias<0?90:60;
    Bias=Clamp(Bias+input*rate/maxAngle*dt,Math.Min(cap,12/maxAngle));
   }else if(!stale)Bias=Approach(Bias,0,dt,.12);
   output=Clamp(balance+ordinary+Bias,cap);
  }else if(Blending){
   Bias=Approach(Bias,0,dt,.12);output=Approach(output,ordinary,dt,.08);
   if(Math.Abs(output-ordinary)<.002&&Math.Abs(Bias)<.002){Reset();output=ordinary;}
  }else output=ordinary;
  return output;
 }
}


