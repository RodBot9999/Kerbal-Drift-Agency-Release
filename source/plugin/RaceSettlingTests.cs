using System;
using System.IO;
using System.Globalization;
class RaceSettlingTests {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static KDACore.Result Run(double slip,double yaw,double weight,int mode=2,double rate=0){return KDACore.Evaluate(mode,1,8-6.5*weight,78,0,40,2.65,9.81,slip,yaw,0,0,true,false,3,50,rate,yaw,0,weight);}
 static void Main(string[] paths){
  var s=new RaceNeutralSettling();
  for(int i=0;i<100;i++)s.Step(.02,true,0,.1,.01,false);
  Check(s.Weight==0,"no intervention on stable straight driving");
  for(int i=0;i<8;i++)s.Step(.02,true,0,9,-.3,true);
  // Prior quiet release time is legitimate: controller can engage on new
  // measured instability even if keys have already been released a while.
  Check(s.Weight>0&&s.Weight<1,"disturbance builds gradual response");
  for(int i=0;i<50;i++)s.Step(.02,true,0,9,-.3,true);
  Check(s.Weight>.98,"sustained neutral-input slide engages settling");
  double before=s.Weight;s.Step(.02,true,1,9,-.3,true);
  Check(s.Weight<before*.57,"new driver input withdraws settling rapidly");
  for(int i=0;i<30;i++)s.Step(.02,true,1,9,-.3,true);
  Check(s.Weight<1e-6,"held driver input regains original tolerance");
  var taps=new RaceNeutralSettling();
  for(int i=0;i<200;i++)taps.Step(.02,true,i%8<3?1:0,9,-.3,true);
  Check(taps.Weight==0,"frequent keyboard taps retain driver authority");
  var release=new RaceNeutralSettling();
  for(int i=0;i<8;i++)release.Step(.02,true,0,9,-.3,true);
  Check(release.Weight==0,"release pause prevents abrupt counterstep");
  for(int i=0;i<60;i++)release.Step(.02,true,0,9,-.3,true);
  for(int i=0;i<80;i++)release.Step(.02,true,0,.2,.01,false);
  Check(release.Weight<1e-4,"settled motion withdraws response without integral memory");
  release.Step(.02,false,0,9,-.3,true);Check(release.Weight==0,"mode/standby resets settling");
  release.Step(.02,true,0,Double.NaN,0,true);Check(release.Weight==0,"invalid motion resets safely");
  foreach(double sign in new[]{-1.0,1.0}){
   var old=Run(sign*8,sign*-.25,0);var updated=Run(sign*8,sign*-.25,1);
   Check(Math.Abs(updated.Correction)>Math.Abs(old.Correction)+.1,"persistent mild slide receives meaningful steering");
   Check(updated.Correction*sign<0,"settling corrects slide in both directions");
   Check(updated.Limit==old.Limit,"ordinary cornering budget unchanged");
   Check(updated.RecoveryLimit<=.8,"Race recovery cap remains 32 degrees");
   var convergence=Run(sign*8,0,1,2,-sign*100);
   Check(convergence.Correction==0,"convergence prediction never kicks into opposite slip");
  }
  var a=KDACore.Evaluate(3,1,12,78,0,65,2.65,9.81,15,-.3,0,0,true,false,3,50,0,-.3,0,0);
  var b=KDACore.Evaluate(3,1,12,78,0,65,2.65,9.81,15,-.3,0,0,true,false,3,50,0,-.3,0,1);
  Check(a.Correction==b.Correction&&a.Limit==b.Limit,"Drift correction unaffected by settling parameter");
  var brakes=new AidBrakeCore();double pressure=0;
  for(int i=0;i<10;i++)pressure=brakes.Step(.02,false,true,0,.04);
  Check(Math.Abs(pressure-.04)<1e-9,"SC brakes available with TCS off");
  Check(brakes.Step(.02,false,false,0,.04)==0,"SC off removes its pressure immediately");
  for(int i=0;i<10;i++)pressure=brakes.Step(.02,true,false,.08,0);
  Check(Math.Abs(pressure-.08)<1e-9,"TCS brakes work independently of SC");
  Check(brakes.Step(.02,false,false,.08,.04)==0,"all aids off removes both pressures");
  for(int i=0;i<10;i++)brakes.Step(.02,true,true,.08,.04);
  Check(Math.Abs(brakes.Step(.02,false,true,.08,.04)-.04)<1e-9,"TCS toggle preserves SC and clears traction pressure");
  Check(brakes.Step(.02,false,false,0,0)==0,"Drift brake bypass state clears");
  // Recorded-motion replay measures engagement only; vehicle dynamics are
  // not simulated, so it does not establish real closed-loop recovery.
  foreach(string path in paths){
   var lines=File.ReadAllLines(path);var header=lines[0].Split(',');var settling=new RaceNeutralSettling();int neutral=0,active=0;
   foreach(string line in lines){if(line==lines[0]||line.Length==0)continue;var row=line.Split(',');
    Func<string,double> n=k=>Double.Parse(row[Array.IndexOf(header,k)],CultureInfo.InvariantCulture);
    bool eligible=n("sc_mode")==2&&n("sc_enabled")==1&&n("forward_mps")>=12&&n("grounded_wheels")>=2&&n("burnout")==0;
    double weight=settling.Step(.02,eligible,n("raw_steer"),n("slip_deg"),n("yaw_rad_s"),n("sc_active")==1);
    if(eligible&&Math.Abs(n("raw_steer"))<.02&&Math.Abs(n("slip_deg"))>5){neutral++;if(weight>.8)active++;}
   }
   Check(active>0&&neutral>0,"engages on recorded neutral-input slide");
   Console.WriteLine(Path.GetFileName(Path.GetDirectoryName(path))+": "+active+" of "+neutral+" neutral-slide samples at >80% settling");
  }
  Console.WriteLine("PASS "+checks+" settling/independent-brake checks");
 }
}
