using System;
namespace KerbalDriftAgency {
 public static class DrivelineCore {
  public static double Pedal(double current,double target,double dt,double rise,double release){
   target=Math.Max(-1,Math.Min(1,target));dt=Math.Max(0,dt);
   if(current*target<0)target=0;
   double seconds=Math.Abs(target)>Math.Abs(current)?rise:release;
   if(seconds<=0)return target;
   double step=dt/seconds;
   return current+Math.Max(-step,Math.Min(step,target-current));
  }
  // Transfer excess wheelspin, not the normal speed difference in a turn.
  // Road LSD modes use 2:1 bias; Drift uses 4:1 and earlier engagement.
  public static double LeftShare(double leftWeight,double rightWeight,double leftSlip,double rightSlip,int mode){
   double total=leftWeight+rightWeight;if(total<=0)return .5;
   double baseShare=leftWeight/total;if(mode<=0)return baseShare;
   double difference=leftSlip-rightSlip;
   bool drift=mode==3;
   double excess=Math.Max(0,Math.Abs(difference)-(drift?.015:.08));
   double transfer=Math.Min(drift?.30:1.0/6,excess*(drift?1.2:mode==2?.55:.30));
   double minimum=drift?.20:1.0/3;
   return Math.Max(minimum,Math.Min(1-minimum,baseShare-Math.Sign(difference)*transfer));
  }
 }
}
