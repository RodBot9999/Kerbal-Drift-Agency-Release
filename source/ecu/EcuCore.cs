// SPDX-License-Identifier: MIT
using System;
namespace KerbalDriftAgency.ECU {
 public struct WheelPoint {public double X,Z;public bool CanSteer;public int Group;}
 public static class EcuCore {
  public static double Clean(double x,double fallback,double min,double max){return double.IsNaN(x)||double.IsInfinity(x)?fallback:Math.Max(min,Math.Min(max,x));}
  // Group 0 Auto, 1 Front, 2 Rear, 3 Ignore. The ECU may be mounted off-centre.
  public static int[] FrontPair(WheelPoint[] wheels){
   double minX=double.MaxValue,maxX=double.MinValue,minZ=double.MaxValue,maxZ=double.MinValue;
   foreach(var w in wheels)if(w.Group!=3){minX=Math.Min(minX,w.X);maxX=Math.Max(maxX,w.X);minZ=Math.Min(minZ,w.Z);maxZ=Math.Max(maxZ,w.Z);}
   double midX=(minX+maxX)*.5,midZ=(minZ+maxZ)*.5;int left=-1,right=-1;
   for(int i=0;i<wheels.Length;i++){var w=wheels[i];if(!w.CanSteer||w.Group==2||w.Group==3||w.Group==0&&w.Z<midZ)continue;
    if(w.X<midX){if(left<0||w.Z>wheels[left].Z)left=i;}else if(right<0||w.Z>wheels[right].Z)right=i;
   }
   return new[]{left,right};
  }
  // Apply the allowance AFTER calculating the conservative baseline. An
  // increased allowance must not be clipped by that same baseline again.
  public static double Budget(double percent,double track,double cgHeight){
   double baseline=Math.Min(.65,Math.Max(.15,.72*Clean(track,1,.1,100)/(2*Clean(cgHeight,.5,.15,100))));
   return baseline*Clean(percent,100,60,160)/100;
  }
 }
}
