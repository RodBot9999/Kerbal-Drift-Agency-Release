using System;

// Observation only. Coordinates are vehicle right, forward, up; positive roll
// is right side down, positive pitch is nose up. Pitch must not masquerade as roll.
public static class TelemetryGeometryCore {
 public static bool Angles(double right,double forward,double up,out double roll,out double pitch){
  roll=pitch=Double.NaN;double length=right*right+forward*forward+up*up;
  if(Double.IsNaN(length)||Double.IsInfinity(length)||length<1e-12)return false;
  roll=Math.Atan2(-right,up)*180/Math.PI;
  pitch=Math.Atan2(forward,Math.Sqrt(right*right+up*up))*180/Math.PI;
  return true;
 }
}
