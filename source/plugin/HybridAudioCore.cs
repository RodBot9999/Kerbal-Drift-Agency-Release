// SPDX-License-Identifier: MIT
using System;
using System.Globalization;
using System.Text.RegularExpressions;
namespace KerbalDriftAgency {
 public static class HybridAudioCore {
  public static readonly string[] Families={"i3","i4","i5","i6","v6","v8_cross","v8_flat","v10","v12","boxer4","boxer6"};
  public static readonly string[] Labels={"AUTO","INLINE 3","INLINE 4","INLINE 5","INLINE 6","V6","V8 CROSS-PLANE","V8 FLAT-PLANE","V10","V12","BOXER 4","BOXER 6"};
  public static readonly double[] Rpms={750,1250,2000,3200,5000,7500,11000,16000};
  // Preserve saved IDs: 0 Standard, 1 Straight pipe. New filters append.
  public static readonly string[] ExhaustLabels={"STANDARD","STRAIGHT PIPE","QUIET MUFFLER","SPORT","DEEP TONE","RACE"};
  public static readonly string[] ExhaustHelp={"Balanced muffling and volume.","Open, bright exhaust; up to 40% louder.","Quieter, rounded exhaust with stronger muffling.","A little louder and brighter, with controlled edges.","Warmer, darker tone with a rounded upper end.","Open race exhaust with more high-frequency detail."};
  static readonly double[] exhaustGain={1,1.4,.70,1.10,.95,1.20};
  static readonly double[] exhaustFilter={1,1.8,.18,1.12,.30,1.55};
  static readonly double[] exhaustQ={.85,.75,.70,.85,1.05,.75};
  public static int ExhaustIndex(int mode){return mode>=0&&mode<ExhaustLabels.Length?mode:0;}
  public static double ExhaustGain(int mode){return exhaustGain[ExhaustIndex(mode)];}
  public static double ExhaustFilter(int mode){return exhaustFilter[ExhaustIndex(mode)];}
  public static double ExhaustQ(int mode){return exhaustQ[ExhaustIndex(mode)];}
  public static double ToneCutoff(double tone,double load,int pipe,double liters,double idleBlend){
   tone=Clean(tone,.55,0,1);load=Clean(load,0,0,1);idleBlend=Clean(idleBlend,0,0,1);
   double character=Math.Pow(Clean(liters,2,.5,10)/2,-.08);
   return Clean((600+3600*tone)*(.62+.38*load)*ExhaustFilter(pipe)*character*(.75+.25*idleBlend),1500,120,8500);
  }
  public static double IdleBlend(double rpm,double idle,double load){
   idle=Clean(idle,900,500,5000);rpm=Clean(rpm,idle,300,24000);load=Clean(load,0,0,1);
   double rise=Math.Max(Clean((rpm/idle-1)/.9,0,0,1),Math.Min(1,load*2));
   return rise*rise*(3-2*rise);
  }
  public static double IdleGain(double rpm,double idle,double load){return .70+.30*IdleBlend(rpm,idle,load);}
  public static double Clean(double value,double fallback,double lo,double hi){return double.IsNaN(value)||double.IsInfinity(value)?fallback:Math.Max(lo,Math.Min(hi,value));}
  // Only called at configuration changes, never in the audio callback.
  public static string Resolve(string layout,string label,int selection){
   if(selection>0&&selection<=Families.Length)return Families[selection-1];
   string text=(layout+" "+label).ToUpperInvariant();
   var match=Regex.Match(text,@"(?:^|[^A-Z0-9])(BOXER|I|V|B|F)[ -]?(\d{1,2})(?:[^0-9]|$)");
   if(!match.Success)return "i4";
   string prefix=match.Groups[1].Value;int n=int.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture);
   if(prefix=="B"||prefix=="F"||prefix=="BOXER")return n==6?"boxer6":n==4?"boxer4":"i4";
   if(prefix=="V")return n==8?"v8_cross":n==6?"v6":n==10?"v10":n==12?"v12":"i4";
   return n==3?"i3":n==5?"i5":n==6?"i6":"i4";
  }
  public static double Displacement(string label,double explicitLiters){
   if(explicitLiters>0)return Clean(explicitLiters,2,.1,20);
   var m=Regex.Match(label??"",@"(\d+(?:\.\d+)?)\s*L\b",RegexOptions.IgnoreCase);
   double liters;return m.Success&&double.TryParse(m.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out liters)?Clean(liters,2,.1,20):2;
  }
  public static int Band(double rpm){rpm=Clean(rpm,900,100,24000);int i=0;while(i<Rpms.Length-2&&rpm>Rpms[i+1])i++;return i;}
  public static double Blend(double rpm,int band){return Clean((rpm-Rpms[band])/(Rpms[band+1]-Rpms[band]),0,0,1);}
  public static int Cycles(int index){return Math.Max(4,(int)Math.Round(.78*Rpms[index]/120));}
  public static double Pitch(double rpm,int index,int samples,int rate){
   double actual=120.0*Cycles(index)*rate/Math.Max(1,samples);
   return Clean(rpm/actual,1,.4,2);
  }
  public static double Phase(double crankCycles,int index){double n=Cycles(index);return (crankCycles%n+n)%n/n;}
 }
 public sealed class HybridAudioEnvelope {
  public double Rpm=900,Load,Gain,CrankCycles;
  public void Step(double dt,double rpm,double load,bool cut,bool running){
   dt=HybridAudioCore.Clean(dt,0,0,.1);rpm=HybridAudioCore.Clean(rpm,900,300,24000);load=HybridAudioCore.Clean(load,0,0,1);
   CrankCycles+=Rpm*dt/120;
   Rpm+=(rpm-Rpm)*(1-Math.Exp(-dt/.025));Load+=(load-Load)*(1-Math.Exp(-dt/.035));
   double target=running?(cut?.52:1):0;Gain+=(target-Gain)*(1-Math.Exp(-dt/(target<Gain?.012:.045)));
  }
 }
}
