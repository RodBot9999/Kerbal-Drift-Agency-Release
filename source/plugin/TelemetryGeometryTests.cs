using System;
class TelemetryGeometryTests {
 static void Near(double a,double b){if(Math.Abs(a-b)>1e-8)throw new Exception(a+" != "+b);}
 static void Main(){
  double roll,pitch;
  TelemetryGeometryCore.Angles(0,0,1,out roll,out pitch);Near(roll,0);Near(pitch,0);
  TelemetryGeometryCore.Angles(-1,0,1,out roll,out pitch);Near(roll,45);Near(pitch,0);
  TelemetryGeometryCore.Angles(1,0,1,out roll,out pitch);Near(roll,-45);Near(pitch,0);
  TelemetryGeometryCore.Angles(0,.5,Math.Sqrt(.75),out roll,out pitch);Near(roll,0);Near(pitch,30);
  TelemetryGeometryCore.Angles(-.5,.5,Math.Sqrt(.5),out roll,out pitch);Near(roll,35.264389682754654);Near(pitch,30);
  TelemetryGeometryCore.Angles(0,0,-1,out roll,out pitch);Near(Math.Abs(roll),180);Near(pitch,0);
  if(TelemetryGeometryCore.Angles(0,0,0,out roll,out pitch)||TelemetryGeometryCore.Angles(Double.NaN,0,1,out roll,out pitch))throw new Exception("Invalid normals must be rejected");
  Console.WriteLine("PASS: telemetry signed roll, pitch isolation, combined attitude, inversion and invalid reference");
 }
}
