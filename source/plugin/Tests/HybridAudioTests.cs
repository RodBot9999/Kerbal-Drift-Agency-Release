// SPDX-License-Identifier: MIT
// Compile with HybridAudioCore, KDAHybridAudio and the isolated test stubs.
using System;using System.Diagnostics;using System.Reflection;using UnityEngine;using KerbalDriftAgency;
public static class HybridAudioTests {
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Step(KDAHybridAudio[] players,float dt,float volume){Time.realtimeSinceStartup+=dt;AudioSettings.dspTime+=dt;foreach(var p in players)p.Tick(volume,.55f,0,0,.55f);}
 static void Dead(KDAHybridAudio player){typeof(KDAHybridAudio).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,null);}
 public static void Main(){
  Check(HybridAudioCore.Resolve("V8","",0)=="v8_cross","V8 default");
  Check(HybridAudioCore.Resolve("","I5 2.3 L",0)=="i5","saved engine label inference");
  Check(HybridAudioCore.Resolve("B4","",0)=="boxer4","boxer");
  Check(HybridAudioCore.Resolve("V8","",7)=="v8_flat","explicit flat plane");
  Check(HybridAudioCore.Resolve("arbitrary } bad","",0)=="i4","safe unknown voice");
  Check(Math.Abs(HybridAudioCore.Displacement("V8 5.25 L - Car",0)-5.25)<1e-6,"displacement");
  Check(Math.Abs(HybridAudioCore.IdleGain(900,900,0)-.70)<1e-8,"quieter unloaded idle");
  Check(Math.Abs(HybridAudioCore.IdleGain(1700,1700,0)-.70)<1e-8,"racing idle follows imported idle calibration");
  Check(HybridAudioCore.IdleGain(1700,1700,1)==1,"loaded low RPM retains volume");
  Check(HybridAudioCore.IdleGain(4500,900,0)==1,"high RPM coast retains volume");
  Check(HybridAudioCore.ExhaustGain(1)==1.4&&HybridAudioCore.ExhaustIndex(-1)==0,"legacy exhaust IDs and invalid fallback");
  Check(HybridAudioCore.ExhaustFilter(2)<HybridAudioCore.ExhaustFilter(4)&&HybridAudioCore.ExhaustFilter(4)<HybridAudioCore.ExhaustFilter(0),"quiet/deep muffling");
  Check(HybridAudioCore.ExhaustFilter(3)>HybridAudioCore.ExhaustFilter(0)&&HybridAudioCore.ExhaustFilter(5)>HybridAudioCore.ExhaustFilter(3),"sport/race filters");
  Check(HybridAudioCore.ToneCutoff(.5,0,2,5,0)<250,"quiet muffler reaches audible idle body frequencies");
  Check(HybridAudioCore.ToneCutoff(.5,0,4,5,0)<HybridAudioCore.ToneCutoff(.5,0,0,5,0)*.4,"deep filter distinctly darker than standard");
  Check(HybridAudioCore.ToneCutoff(.5,1,1,5,1)>HybridAudioCore.ToneCutoff(.5,1,0,5,1)*1.5,"straight pipe distinctly more open");
  Check(HybridAudioCore.ToneCutoff(0,0,0,5,0)<HybridAudioCore.ToneCutoff(1,0,0,5,0)/4,"tone sweep affects audible spectrum");
  for(int rpm=300;rpm<=24000;rpm+=17){int lo=HybridAudioCore.Band(rpm);double t=HybridAudioCore.Blend(rpm,lo);Check(lo>=0&&lo<7&&t>=0&&t<=1,"bank range");}
  var envelope=new HybridAudioEnvelope();for(int i=0;i<500;i++)envelope.Step(1.0/60,6000,1,false,true);
  Check(Math.Abs(envelope.Rpm-6000)<.01&&envelope.Gain>.99,"smooth acceleration");
  envelope.Step(.05,6000,double.NaN,true,true);Check(envelope.Gain<.6&&!double.IsNaN(envelope.Load),"limiter and invalid load");
  var players=new KDAHybridAudio[24];
  for(int i=0;i<players.Length;i++){var part=new Part();part.vessel=new Vessel();part.engine=new KDAEngine{part=part,engineRunning=true,engineRpm=4500,AudioThrottle=.7f,importedEngineName="V8 5.25 L"};part.transform.position=new Vector3(i*5,0,0);players[i]=part.gameObject.AddComponent<KDAHybridAudio>();players[i].Initialize(part,"",0);if(i==0)FlightGlobals.ActiveVessel=part.vessel;}
  Check(GameObject.MissingAudioFilterDependencies==0,"Unity audio-filter dependency satisfied during initialization");
  var interruptedPart=new Part();interruptedPart.vessel=new Vessel();interruptedPart.engine=new KDAEngine{part=interruptedPart,engineRunning=true,engineRpm=4500,AudioThrottle=.7f,importedEngineName="B6 3.83 L"};
  var interrupted=interruptedPart.gameObject.AddComponent<KDAHybridAudio>();GameObject.RejectFilters=true;bool failedStart=false;
  try{interrupted.Initialize(interruptedPart,"",0);}catch(NullReferenceException){failedStart=true;}finally{GameObject.RejectFilters=false;}
  Check(failedStart,"simulate interrupted native component initialization");
  interrupted.Tick(1,.55f,0,0,.55f);Check(!interrupted.Playing,"partial initialization cannot enter playback or repeat layer exceptions");Dead(interrupted);
  for(int i=0;i<30;i++)Step(players,1f/60,1);
  Check(KDAHybridAudio.AudibleCars==4,"global voice budget");
  Check(AudioSource.PlayingCombustion==48,"twelve synchronized stem loops per audible car");
  Check(GameDatabase.Instance.RequestCount("KDAVehicles/Sounds/Hybrid/stems/v8_cross/body/rpm_0_0")==1,"bank cache is shared");
  var filter=(AudioLowPassFilter)typeof(KDAHybridAudio).GetField("filter",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(players[0]);
  for(int i=0;i<30;i++){Time.realtimeSinceStartup+=.02f;AudioSettings.dspTime+=.02;players[0].Tick(1,.55f,2,0,.55f);}float quiet=filter.cutoffFrequency;
  for(int i=0;i<30;i++){Time.realtimeSinceStartup+=.02f;AudioSettings.dspTime+=.02;players[0].Tick(1,.55f,5,0,.55f);}float race=filter.cutoffFrequency;
  Check(race>quiet*2,"garage preset changes native filter cutoff");
  Check(AudioSource.PlayingCombustion<=48,"filters reuse existing playback voices");
  var voices=(AudioSource[])typeof(KDAHybridAudio).GetField("voices",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(players[0]);
  players[0].SetMix(0,1,1,0,0,0,.70f);
  for(int i=0;i<80;i++)Step(players,.02f,1);
  for(int i=0;i<voices.Length;i+=3){Check(voices[i].volume<.0001f,"body can be isolated/muted");Check(voices[i+1].volume>0||voices[i+2].volume>0,"other stems remain independent");}
  players[0].SetMix(1,0,0,1,.55f,1,.70f);for(int i=0;i<80;i++)Step(players,.02f,1);
  for(int i=0;i<voices.Length;i+=3){Check(voices[i].volume>0,"body preserved with accents muted");Check(voices[i+1].volume<.0001f&&voices[i+2].volume<.0001f,"pulse and texture can mute");}
  players[0].SetMix(float.NaN,float.PositiveInfinity,1,1,.55f,1,.70f);for(int i=0;i<80;i++)Step(players,.02f,1);
  Check(AudioSource.PlayingCombustion==48,"invalid settings safely restore defaults");
  int created=AudioSource.All.Count,clips=AudioClip.Created;
  var clock=Stopwatch.StartNew();for(int i=0;i<6000;i++)Step(players,1f/60,1);clock.Stop();
  Check(AudioSource.All.Count==created&&AudioClip.Created==clips,"no per-frame sources or clips");
  TimeWarp.CurrentRate=5;Step(players,1f/60,1);Check(AudioSource.PlayingTotal==0,"warp stops all engine/detail layers");
  TimeWarp.CurrentRate=1;for(int i=0;i<5;i++)Step(players,1f/60,1);Check(AudioSource.PlayingCombustion==48,"warp resume");
  PauseMenu.isOpen=true;Step(players,1f/60,1);Check(AudioSource.PlayingTotal==0,"pause");PauseMenu.isOpen=false;
  MapView.MapIsEnabled=true;Step(players,1f/60,1);Check(AudioSource.PlayingTotal==0,"map");MapView.MapIsEnabled=false;
  for(int i=0;i<5;i++)Step(players,1f/60,1);Step(players,1f/60,0);Check(AudioSource.PlayingTotal==0,"mute stops playback");
  // Scheduled playback must not be continually pushed forward above 100 FPS.
  for(int i=0;i<20;i++)Step(players,.002f,1);Check(AudioSource.PlayingCombustion==48,"500 FPS scheduled start");
  players[0].Tick(1,.5f,0,7,.55f);Step(players,.02f,1);Check(AudioSource.PlayingCombustion<=48,"voice override does not leak loops");
  foreach(var player in players)Dead(player);Check(AudioSource.PlayingTotal==0,"destroy cleanup");
  Console.WriteLine("PASS "+checks+" checks; 24 cars x 6000 frames in "+clock.ElapsedMilliseconds+" ms (managed stub benchmark, not KSP FPS); zero new clips/sources during steady playback.");
 }
}
