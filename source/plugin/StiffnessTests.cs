using System;
class StiffnessTests {
 static int n;static void Check(bool v,string s){if(!v)throw new Exception(s);n++;}
 static void Main(){
  double[] oldFront={90,135,202.5},oldRear={82.5,120,180},oldBars={24,39,63};
  for(int mode=0;mode<3;mode++){
   double f=mode==2?1.3:1.25;var s=KDACore.Preset(mode);
   Check(Math.Abs(s.FrontSpring/oldFront[mode]-f)<1e-9,"front factor");Check(Math.Abs(s.RearSpring/oldRear[mode]-f)<1e-9,"rear factor");Check(Math.Abs(s.FrontBar/oldBars[mode]-f)<1e-9,"bar factor");
   Check(s.FrontSpring<=280&&s.RearSpring>=93.75,"preset unclamped");
   foreach(double velocity in new[]{-.3,-.02,.02,.3}){
    double now=SuspensionCore.Damper(s.FrontSpring,.35,velocity,s.Bump,s.Rebound,s.FastBump,s.FastRebound);
    double before=SuspensionCore.Damper(oldFront[mode],.35,velocity,s.Bump/Math.Sqrt(f),s.Rebound/Math.Sqrt(f),s.FastBump/Math.Sqrt(f),s.FastRebound/Math.Sqrt(f));
    Check(Math.Abs(now/before-f)<1e-9,"physical damper scales with spring factor");
   }
  }
  Console.WriteLine(n+" stiffness checks passed");
 }
}
