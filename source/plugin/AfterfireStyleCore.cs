using System;
namespace KerbalDriftAgency {
 public static class AfterfireSoundCore {
  public const int Rate=22050;
  // Original pressure/noise transients with short pipe reflections. Avoid the
  // old clean sine thump and sharp differentiated white-noise click.
  public static float[] Samples(bool bang,int variation){
   int length=(int)(Rate*(bang?.32:.16));var data=new float[length];var pressure=new double[length];var random=new Random((bang?7429:3951)+variation*193);
   double low=0,body=0,dc=0;double peak=0;int reflection=(int)(Rate*(.0045+.0007*(variation%3)));
   for(int i=0;i<length;i++){
    double t=(double)i/Rate,n=random.NextDouble()*2-1;
    low+=.24*(n-low);body+=.055*(n-body);
    pressure[i]=(.72*low+.46*body)*Math.Exp(-t/(bang?.028:.012));
    double p=pressure[i];for(int tap=1;tap<=4;tap++){int j=i-tap*reflection;if(j>=0)p+=pressure[j]*Math.Pow(-.48,tap);}
    dc+=.012*(p-dc);p=Math.Tanh((p-dc)*(bang?3.3:2.5));
    double attack=Math.Min(1,t/.0006),fade=Math.Min(1,(length-1-i)/(Rate*.012));
    data[i]=(float)(p*attack*fade);peak=Math.Max(peak,Math.Abs(data[i]));
   }
   double scale=(bang?.52:.42)/Math.Max(.001,peak);for(int i=0;i<length;i++)data[i]*=(float)scale;return data;
  }
  public static double TuneGain(int tune){return tune==2?.05:tune==1?.65:.75;}
 }
 public sealed class FlamePlumeCore {
  double intensity,tail,previousThrottle;
  public double Step(double dt,bool running,int tune,double throttle,double rpm,bool shifting,double amount,double duration){
   dt=Clean(dt,0,0,.1);throttle=Clean(throttle,0,0,1);rpm=Clean(rpm,0,0,30000);amount=Clean(amount,0,0,3);duration=Clean(duration,1,.5,3);
   if(!running||tune!=2||amount==0){intensity=tail=0;previousThrottle=throttle;return 0;}
   tail=Math.Max(0,tail-dt);
   if(previousThrottle>.4&&throttle<.15&&rpm>3000)tail=.65*duration;
   if(shifting&&rpm>3000)tail=Math.Max(tail,.2*duration);
   previousThrottle=throttle;
   double target=rpm>2800&&throttle>.45?(.45+.55*throttle)*Math.Min(1,amount):rpm>2200&&tail>0?.75*Math.Min(1,amount)*Math.Min(1,tail/.2):0;
   intensity+=(target-intensity)*Math.Min(1,dt/(target>intensity?.08:.18));return intensity<.001?0:intensity;
  }
  static double Clean(double x,double fallback,double lo,double hi){return double.IsNaN(x)||double.IsInfinity(x)?fallback:Math.Max(lo,Math.Min(hi,x));}
 }
}
