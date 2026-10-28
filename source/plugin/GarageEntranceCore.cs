using System;
namespace KerbalDriftAgency {
 public static class GarageEntranceCore {
  // Distances in world metres, resolved in the actual door's frame.
  public static bool Contains(double right,double outward,double height,double doorHalfWidth){
   return !double.IsNaN(right)&&!double.IsNaN(outward)&&!double.IsNaN(height)&&
    outward>=.25&&outward<=18&&Math.Abs(right)<=Math.Max(3,doorHalfWidth+3)&&Math.Abs(height)<6;
  }
 }
}
