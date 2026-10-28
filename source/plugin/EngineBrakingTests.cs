using System;
using KerbalDriftAgency;
class EngineBrakingTests {
 static int count;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
 static double Coast(double rpm,double ratio,int wheels,double pedal=0,double scale=1){return TuningCore.Drag(rpm/(ratio*3.89),ratio,3.89,6500,900,pedal,true,wheels,scale);}
 static double Old(double rpm){return (8+85*Math.Pow(rpm/6500,2))*EngineCore.Clamp((rpm-900)/1200,0,1);}
 static void Main(){
  foreach(double rpm in new[]{2500.0,4000.0,6000.0,6500.0}){
   double loss=(Coast(rpm,1,2)*1000-8)*2/(3.89*.90);
   Check(loss>Old(rpm)*2,"more than doubled coast torque at "+rpm);
  }
  Check(Coast(6000,1,2)>Coast(2500,1,2)*2,"RPM increases engine braking");
  Check(Coast(6000,2,2)>Coast(6000,1,2)*1.9,"lower gear multiplies engine torque");
  Check(Math.Abs((Coast(6000,1,4)*1000-8)*4-(Coast(6000,1,2)*1000-8)*2)<1e-8,"AWD divides total drag rather than doubles it");
  Check(Coast(6000,1,2,1)==Coast(6000,1,2,.12),"open accelerator leaves only rolling resistance");
  Check(Coast(6000,1,2,.06)<Coast(6000,1,2,0),"partial lift fades braking progressively");
  Check(Coast(900,1,2)<.009,"idle does not stall axle");
  Check(Coast(6000,1,2,0,2)>Coast(6000,1,2,0,1)*1.9,"engine-size scaling affects coast torque");
  Check(TuningCore.Drag(1000,1,3.89,6500,900,0,false,2)==.008,"disconnected clutch retains rolling resistance only");
  Check(TuningCore.Drag(0,3.5,3.89,6500,900,0,true,2)==0,"stationary car receives zero drag");
  Check(TuningCore.Drag(-500,-1,3.89,6500,900,0,true,2)>0,"reverse dissipates energy");
  Console.WriteLine("PASS "+count+" engine-braking checks");
 }
}
