using System;

// Native KSP physics uses tonnes and kN. N/mm has the same numerical value
// as kN/m; damping below is in kN s/m. No independent tire solver is added.
public static class SuspensionCore {
 public static double Clamp(double x,double lo,double hi){return Math.Max(lo,Math.Min(hi,x));}
 public static double Damper(double spring,double mass,double velocity,double bump,double rebound,double highBump,double highRebound){
  double blend=Clamp((Math.Abs(velocity)-.05)/.20,0,1);blend=blend*blend*(3-2*blend);
  double low=velocity>=0?bump:rebound,high=velocity>=0?highBump:highRebound;
  return 2*Math.Sqrt(Math.Max(.001,spring*mass))*(low+(high-low)*blend);
 }
 // A torsion bar couples spring compression. Limit its preload to one quarter
 // of available travel; never depend on an already-unloaded tire's load.
 public static double BarCapacity(double leftSpring,double rightSpring,double travel){return Math.Max(0,Math.Min(leftSpring,rightSpring))*Math.Max(0,travel)*.25;}
 public static double CoupledBar(double leftDepth,double rightDepth,double rate,double leftSpring,double rightSpring,double travel){
  double cap=BarCapacity(leftSpring,rightSpring,travel);
  return Clamp((Clamp(leftDepth,0,travel)-Clamp(rightDepth,0,travel))*Clamp(rate,0,120),-cap,cap);
 }
 public static double Anchor(double travel,double heightMm){return Clamp((.04+heightMm*.001)/travel,.05,.95);}
 // Parked hub rests at the attachment reference: compensate sag and the native
 // minimum preload position (10% of 160mm), rather than preserving old sag.
 public static double ReferenceOffset(double heightMm,double staticLoad,double spring){return heightMm*.001+staticLoad/spring+.016-.04;}
}
