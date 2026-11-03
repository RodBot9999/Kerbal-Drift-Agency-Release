using System;
class ImportedSteeringTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static KDACore.Result Run(double capacity,double roll,double load){return KDACore.Evaluate(2,1,8,45,1,40,2.7,9.81,0,0,roll,load,true,false,capacity);}
 static void Main(){
  double street=KDACore.ImportedCapacity(1.15,0,1000,9.81);
  double sticky=KDACore.ImportedCapacity(2.3,0,1000,9.81);
  double aero=KDACore.ImportedCapacity(2.3,3000,1000,9.81);
  Check(street==1,"Reference grip preserves old calibration");
  Check(sticky>street&&aero>sticky,"Front grip and aero increase corner allowance");
  Check(Run(aero,0,0).Limit>Run(street,0,0).Limit,"Useful angle grows with available grip");
  Check(Run(aero,18,.9).Limit<Run(aero,0,0).Limit,"Roll/unloading protection remains");
  Check(Run(aero,0,0).Limit<.1,"High-speed steering remains bounded");
  Check(KDACore.ImportedCapacity(50,1e9,1000,9.81)==3,"Capacity ceiling");
  Check(KDACore.ImportedCapacity(Double.NaN,0,1000,9.81)==1,"Invalid input fallback");
  var old=KDACore.Evaluate(2,1,8,45,1,40,2.7,9.81,0,0,0,0,true,false);
  Check(old.Limit==Run(1,0,0).Limit&&old.Steering==Run(1,0,0).Steering,"default control path unchanged");
  Check(KDACore.CorneringScale(true,2,1,0)==1.25,"Race increased 25 percent");
  Check(KDACore.CorneringScale(true,1,1,0)==1.10,"Sport increased 10 percent");
  Check(KDACore.CorneringScale(false,2,1,0)==1,"default calibration unchanged");
  Check(Math.Abs(KDACore.CorneringScale(true,2,2,20)-3)<1e-9,"Per-car positive adjustment");
  Check(Math.Abs(KDACore.CorneringScale(true,2,2,-20)-2)<1e-9,"Per-car negative adjustment");
  double speed=100/3.6;
  var previous100=KDACore.Evaluate(2,1,8,speed,1,40,2.7,9.81,0,0,0,0,true,false,1);
  var new100=KDACore.Evaluate(2,1,8,speed,1,40,2.7,9.81,0,0,0,0,true,false,KDACore.CorneringScale(true,2,1,0));
  Check(new100.Limit>previous100.Limit*1.24,"More turn-in near 100 km/h");
  var risk100=KDACore.Evaluate(2,1,8,speed,1,40,2.7,9.81,0,0,18,.9,true,false,1.25);
  Check(risk100.Limit<new100.Limit,"Protection still reduces raised allowance");
  Console.WriteLine("PASS: imported grip/aero steering scaling, safety bounds, fallback and legacy calibration");
 }
}
