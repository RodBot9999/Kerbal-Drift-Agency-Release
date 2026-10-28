using System;
namespace KerbalDriftAgency {
 public static class LightingCore {
  public static double Signal(string name,bool headlights,bool high,bool brake,bool reverse,int indicator,double time,bool ignition){
   bool blink=time-Math.Floor(time/.8)*.8<.44;
   switch(name){
    case "lowbeam":case "lowhighbeam":case "running":case "runninglights":return headlights?1:0;
    case "highbeam":return headlights&&high?1:0;
    case "brake":case "brakelights":return brake?1:0;
    case "reverse":return reverse?1:0;
    case "signal_L":return blink&&(indicator==1||indicator==3)?1:0;
    case "signal_R":return blink&&(indicator==2||indicator==3)?1:0;
    case "hazard":return blink&&indicator==3?1:0;
    case "ignitionLevel":return ignition?1:0;
    default:return 0;
   }
  }
 }
}
