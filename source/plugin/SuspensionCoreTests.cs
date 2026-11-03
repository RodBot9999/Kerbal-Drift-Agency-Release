using System;
class SuspensionCoreTests {
 static int count;
 static void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
 static void Main(){
  Check(SuspensionCore.Damper(65,.4,-.01,.45,.7,.3,.5)>SuspensionCore.Damper(65,.4,.01,.45,.7,.3,.5),"Rebound stronger than bump");
  Check(SuspensionCore.Damper(65,.4,.5,.45,.7,.3,.5)<SuspensionCore.Damper(65,.4,.01,.45,.7,.3,.5),"Fast bump softens impact");
  Check(Math.Abs(SuspensionCore.CoupledBar(.03,.03,26,120,120,.16))<1e-9,"Heave has no bar force");
  Check(SuspensionCore.CoupledBar(.04,.02,26,120,120,.16)>0,"Compressed side receives additional spring load");
  Check(SuspensionCore.CoupledBar(.04,.02,26,120,120,.16)==-SuspensionCore.CoupledBar(.02,.04,26,120,120,.16),"Bar reaction antisymmetric");
  Check(SuspensionCore.CoupledBar(.1,0,60,120,120,.16)>3,"One-contact travel retains useful bar authority");
  Check(SuspensionCore.CoupledBar(0,0,60,120,120,.16)==0,"Both drooped means zero bar");
  for(int i=0;i<=160;i++)for(int j=0;j<=160;j++){
   double force=SuspensionCore.CoupledBar(i*.001,j*.001,120,100,280,.16);
   Check(Math.Abs(force/100)<=.0400001&&Math.Abs(force/280)<=.0400001,"Both preload displacements stay within travel bound");
  }
  Check(Math.Abs(SuspensionCore.Anchor(.16,20)*.16-SuspensionCore.Anchor(.16,0)*.16-.02)<1e-9,"Ride offset changes spring reference by exactly 20mm");
  Check(SuspensionCore.Damper(65,.8,.01,.45,.7,.3,.5)>SuspensionCore.Damper(65,.4,.01,.45,.7,.3,.5),"Damping follows mass");
  double before=SuspensionCore.Damper(65,.4,.04999,.45,.7,.3,.5),after=SuspensionCore.Damper(65,.4,.05001,.45,.7,.3,.5);
  Check(Math.Abs(before-after)<.001,"Damping knee continuous");
  double load=4.5;
  foreach(double spring in new[]{30.0,65.0,140.0})Check(Math.Abs(.04+SuspensionCore.ReferenceOffset(0,load,spring)-load/spring-.016)<1e-9,"Static equilibrium matches mounting reference independent of spring rate");
  Console.WriteLine("SuspensionCore: "+count+" checks passed");
 }
}
