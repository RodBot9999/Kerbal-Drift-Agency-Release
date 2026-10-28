using System;using System.Collections.Generic;using System.Globalization;using System.IO;
class TelemetryDiskTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Main(string[] args){
  CultureInfo.CurrentCulture=new CultureInfo("es-MX");Check(TelemetryCsv.Row(1.25,"a,b","a\"b","line\nnext",true)=="1.25,\"a,b\",\"a\"\"b\",\"line\nnext\",1\n","Invariant CSV quoting and decimal values");
  string folder=Path.Combine(args[0],Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  var d=new TelemetryDisk(folder,new Dictionary<string,string>{{"car.csv","sample,value\n"},{"wheel.csv","sample,id\n"}});
  for(int i=0;i<100;i++){d.Add("car.csv",TelemetryCsv.Row(i,i*.02));d.Add("wheel.csv",TelemetryCsv.Row(i,42));}
  d.Stop("Test stop");Check(d.Wait(3000)&&d.Completed,"Queued records drain on stop");
  Check(d.Dropped==0&&d.Error=="","No dropped records or writer failures");
  Check(File.ReadAllLines(Path.Combine(folder,"car.csv")).Length==101,"CSV header and all samples saved");
  Check(File.ReadAllText(Path.Combine(folder,"summary.txt")).Contains("Test stop"),"Stop reason retained");
  d.Add("car.csv","ignored\n");d.Stop("Again");Check(File.ReadAllLines(Path.Combine(folder,"car.csv")).Length==101,"Repeated stops and late records safe");
  Console.WriteLine("PASS: invariant CSV, bounded background queue, paired files, drain, summary and repeated stop");
 }
}
