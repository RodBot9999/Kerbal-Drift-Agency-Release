using System;using KerbalDriftAgency;
class GroundedSmokeTests {
 static int checks;static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Main(){
  var e=new TireSmokeEnvelope();for(int i=0;i<200;i++)e.Step(.02,true,40,40,.5);Check(e.Strength<.001,"normal high speed rolling stays smokeless");
  for(int i=0;i<200;i++)e.Step(.02,true,-40,40,.5);Check(e.Strength<.001,"mirrored imported wheel axes do not create false rolling smoke");
  for(int i=0;i<200;i++)e.Step(.02,true,-40,-40,.5);Check(e.Strength<.001,"clean reverse rolling stays smokeless");
  for(int i=0;i<10;i++)e.Step(.02,true,45,40,10);double brief=e.Strength;for(int i=0;i<100;i++)e.Step(.02,true,45,40,10);Check(e.Strength>brief*2,"sustained drift builds denser smoke than short scrub");Check(!e.Burnout,"moving slide uses low smoke profile");
  e.Step(.02,false,150,0,50);Check(e.Strength==0&&e.TrailBuild==0&&!e.Burnout,"airborne wheel immediately loses all emission and buildup");
  for(int i=0;i<100;i++)e.Step(.02,true,40,0,0);Check(e.Burnout&&e.Strength>.8,"stationary spinning wheel builds burnout cloud");
  for(int i=0;i<150;i++)e.Step(.02,true,20,20,0);Check(e.Strength<.001,"returning to clean rolling stops the trail");
  double driftH=.08,driftV=.02,burnH=.08,burnV=.12;
  for(int i=0;i<600;i++){double dt=.01;driftV=GroundSmokeMotion.Vertical(driftH,driftV,i*dt,false,dt);driftH+=driftV*dt;burnV=GroundSmokeMotion.Vertical(burnH,burnV,i*dt,true,dt);burnH+=burnV*dt;Check(driftH>=.065&&driftH<.7,"drift smoke remains close to birth ground "+i);}
  Check(burnH>driftH+.4&&burnH<2.5,"burnout rises into a modest cloud while drift stays low");
  Check(GroundSmokeMotion.Vertical(3,1,1,false,.02)<0,"over-height drift puff settles rather than rising");Check(GroundSmokeMotion.Vertical(0,-1,1,false,.02)>=0,"puff below floor cannot accelerate farther underground");
  Console.WriteLine("PASS total="+checks);
 }
}
