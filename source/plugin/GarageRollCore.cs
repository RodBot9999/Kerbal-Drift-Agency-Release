using System;

// Editor analysis only: quasi-static sprung-body roll about an explicitly
// assumed roll axis. No forces, tire model, or runtime suspension are changed.
public static class GarageRollCore {
 public struct Input {public double Mass,SprungMass,Height,RollAxis,FrontShare,FrontTrack,RearTrack,FrontSpring,RearSpring,FrontBar,RearBar,Gravity,FrontTravel,RearTravel,FrontBumpStart,RearBumpStart;public bool FrontLong,RearLong;}
 public struct Point {public double Degrees,FrontBarForce,RearBarForce;public bool Valid;}
 public static double Stiffness(double spring,double bar,double track){return (.5*spring+bar)*track*track;}
 public static double Bar(double phi,double rate,double spring,double track,double load,double travel=.16,bool longKit=false){
  double depth=.1*travel+load/Math.Max(1,spring),delta=.5*track*Math.Sin(phi);
  double force=SuspensionCore.CoupledBar(depth+delta,depth-delta,rate,spring,spring,travel);return longKit?Math.Min(force,load*.45):force;
 }
 public static Point Estimate(Input p,double g){
  var result=new Point();if(p.Mass<=0||p.SprungMass<=0||p.Height<=0||p.FrontTrack<.2||p.RearTrack<.2||p.FrontSpring<=0||p.RearSpring<=0||p.Gravity<=0||p.RollAxis<0||p.RollAxis>=p.Height||g<0)return result;
  double arm=p.Height-p.RollAxis,weight=p.Mass*p.Gravity,fw=weight*p.FrontShare/2,rw=weight*(1-p.FrontShare)/2;
  double ft=p.FrontTravel>0?p.FrontTravel:.16,rt=p.RearTravel>0?p.RearTravel:.16;
  double k=Stiffness(p.FrontSpring,0,p.FrontTrack)+Stiffness(p.RearSpring,0,p.RearTrack);
  double stiffness=k+p.FrontBar*p.FrontTrack*p.FrontTrack+p.RearBar*p.RearTrack*p.RearTrack-p.SprungMass*p.Gravity*arm;
  if(stiffness<=0)return result;
  double lo=0,hi=15*Math.PI/180;
  for(int i=0;i<55;i++){
   double phi=(lo+hi)/2,c=Math.Cos(phi),s=Math.Sin(phi);
   double f=Bar(phi,p.FrontBar,p.FrontSpring,p.FrontTrack,fw,ft,p.FrontLong),r=Bar(phi,p.RearBar,p.RearSpring,p.RearTrack,rw,rt,p.RearLong);
   double resist=k*s*c+(f*p.FrontTrack+r*p.RearTrack)*c;
   double moment=p.SprungMass*p.Gravity*arm*(g*c+s);
   if(resist>moment)hi=phi;else lo=phi;
  }
  double angle=(lo+hi)/2;result.FrontBarForce=Bar(angle,p.FrontBar,p.FrontSpring,p.FrontTrack,fw,ft,p.FrontLong);result.RearBarForce=Bar(angle,p.RearBar,p.RearSpring,p.RearTrack,rw,rt,p.RearLong);
  double frontDelta=.5*p.FrontTrack*Math.Sin(angle),rearDelta=.5*p.RearTrack*Math.Sin(angle);
  // Stop the plotted model when its four-loaded-wheel/available-travel
  // assumption breaks. This is not a prediction of a rollover threshold.
  result.Valid=angle<14.99*Math.PI/180&&p.FrontSpring*frontDelta+result.FrontBarForce<fw&&p.RearSpring*rearDelta+result.RearBarForce<rw
   &&.1*ft+fw/p.FrontSpring+frontDelta<ft*(p.FrontLong?p.FrontBumpStart*.01:1)&&.1*rt+rw/p.RearSpring+rearDelta<rt*(p.RearLong?p.RearBumpStart*.01:1);
  result.Degrees=angle*180/Math.PI;return result;
 }
}
