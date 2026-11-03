using System;
namespace KerbalDriftAgency {
 public static class TuningCore {
  public static double[] Ratios(int count,double spacing,double topKmh,double limit,double radius,double finalDrive){
   count=Math.Max(4,Math.Min(6,count));spacing=EngineCore.Clamp(spacing,2,6);
   double top=limit*2*Math.PI*radius*3.6/(60*EngineCore.Clamp(topKmh,140,420)*finalDrive);
   var ratios=new double[count];for(int i=0;i<count;i++)ratios[i]=top*Math.Pow(spacing,1-(double)i/(count-1));return ratios;
  }
  // Positive dissipative torque, in kNm. No direction-dependent drive force.
  public static double Drag(double wheelRpm,double ratio,double finalDrive,double limit,double idle,double throttle,bool connected,int drivenWheels,double engineScale=1){
   wheelRpm=Math.Abs(wheelRpm);
   double rolling=8*EngineCore.Clamp(wheelRpm/30,0,1);
   double reduction=Math.Abs(ratio*finalDrive),rpm=wheelRpm*reduction;
   // Approximate closed-throttle pumping/friction losses. Strong at high RPM,
   // smoothly uncoupled near idle and faded out when the driver opens the pedal.
   // Calibration is deliberately stronger than the old 8+85*x^2 curve;
   // it is a game approximation, not measured coastdown data for each engine.
   double closed=EngineCore.Clamp((.12-throttle)/.12,0,1);
   double compression=connected&&drivenWheels>0
    ?(18+175*Math.Pow(EngineCore.Clamp(rpm/Math.Max(1,limit),0,1),1.6))*EngineCore.Clamp((rpm-idle)/1200,0,1)*closed*EngineCore.Clamp(engineScale,0,4)*reduction*.90/drivenWheels:0;
   return (rolling+compression)/1000;
  }
 }
}
