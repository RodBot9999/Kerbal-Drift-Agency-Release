using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

public static class TelemetryCsv {
 public static string Value(object value){
  if(value==null)return "";
  if(value is bool)return (bool)value?"1":"0";
  if(value is float)return ((float)value).ToString("R",CultureInfo.InvariantCulture);
  if(value is double)return ((double)value).ToString("R",CultureInfo.InvariantCulture);
  var form=value as IFormattable;return form==null?value.ToString():form.ToString(null,CultureInfo.InvariantCulture);
 }
 public static string Row(params object[] values){
  var s=new StringBuilder();for(int i=0;i<values.Length;i++){if(i>0)s.Append(',');string v=Value(values[i]);if(v.IndexOfAny(new[]{',','"','\r','\n'})>=0)s.Append('"').Append(v.Replace("\"","\"\"")).Append('"');else s.Append(v);}return s.Append('\n').ToString();
 }
}

// Only immutable text crosses to this worker. Disk writes cannot block a
// physics step; a bounded queue reports any dropped packets explicitly.
public sealed class TelemetryDisk {
 struct Packet {public string File,Text;}
 readonly BlockingCollection<Packet> queue=new BlockingCollection<Packet>(512);
 readonly Dictionary<string,StreamWriter> writers=new Dictionary<string,StreamWriter>();
 readonly Thread worker;readonly string folder;long dropped;int stopped;
 public long Dropped {get{return Interlocked.Read(ref dropped);}}
 public volatile string Error="";
 public volatile bool Completed;
 string reason="Stopped";
 public TelemetryDisk(string path,Dictionary<string,string> headers){
  folder=path;
  try{foreach(var pair in headers){var w=new StreamWriter(Path.Combine(path,pair.Key),false,new UTF8Encoding(false),65536);writers.Add(pair.Key,w);w.Write(pair.Value);}}
  catch{foreach(var w in writers.Values)w.Dispose();throw;}
  worker=new Thread(Write){IsBackground=true,Name="KDA telemetry writer"};worker.Start();
 }
 public void Add(string file,string text){
  if(Volatile.Read(ref stopped)!=0)return;
  try{if(!queue.TryAdd(new Packet{File=file,Text=text}))Interlocked.Increment(ref dropped);}catch(InvalidOperationException){}
 }
 public void Stop(string why){if(Interlocked.Exchange(ref stopped,1)!=0)return;reason=why;queue.CompleteAdding();}
 public bool Wait(int milliseconds){return worker.Join(milliseconds);}
 void Write(){
  try{var watch=System.Diagnostics.Stopwatch.StartNew();foreach(var p in queue.GetConsumingEnumerable()){writers[p.File].Write(p.Text);if(watch.ElapsedMilliseconds>1000){foreach(var w in writers.Values)w.Flush();watch.Restart();}}}
  catch(Exception e){Error=e.GetType().Name+": "+e.Message;Interlocked.Exchange(ref stopped,1);}
  finally{
   foreach(var w in writers.Values)try{w.Dispose();}catch(Exception e){if(Error=="")Error=e.Message;}
   try{File.WriteAllText(Path.Combine(folder,"summary.txt"),"Stop reason: "+reason+"\nDropped queued packets: "+Dropped+"\nWriter error: "+Error+"\n");}catch{}
   Completed=true;
  }
 }
}
