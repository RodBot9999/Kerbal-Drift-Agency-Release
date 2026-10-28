using System;using System.Collections;using System.Globalization;using ModuleWheels;using UnityEngine;

public sealed partial class KDAGarage {
 int graphPage;float assumedRollAxis;bool presetBusy;GarageRollCore.Input rollInput;string analysisIssue="Load a four-wheel KDA car.";
 double carMass,frontWeight,cgHeight,wheelbase,frontTrack,rearTrack,peakKw,peakTorque;string drive="",engineDescription="",gearDescription="";Part statsBody;
 KerbalDriftAgency.EngineCore ratingSource;bool powerGraph;
 public double EstimatedRollAtOneG {get{RefreshAnalysis();var p=GarageRollCore.Estimate(rollInput,1);return analysisIssue.Length==0&&p.Valid?p.Degrees:double.NaN;}}
 public string AnalysisIssue {get{return analysisIssue;}}
 void RefreshAnalysis(){
  if(body==null||ship==null){analysisIssue="Load a four-wheel KDA car.";return;}
  double mass=0,sprung=0;Vector3 weighted=Vector3.zero;float fy=0,ry=0,fmin=float.MaxValue,fmax=float.MinValue,rmin=float.MaxValue,rmax=float.MinValue,groundZ=0;int nf=0,nr=0;
  foreach(var p in ship.parts){if(p==null)continue;double m=Math.Max(0,p.mass+p.GetResourceMass());mass+=m;weighted+=body.transform.InverseTransformPoint(p.transform.TransformPoint(p.CoMOffset))*(float)m;if(p.FindModuleImplementing<KDASuspension>()==null)sprung+=m;}
  if(mass<=0){analysisIssue="No usable craft mass.";return;}Vector3 cg=weighted/(float)mass;
  foreach(var p in body.children){var s=p.FindModuleImplementing<KDASuspension>();if(s==null)continue;Vector3 pos=body.transform.InverseTransformPoint(p.transform.position);var b=p.FindModuleImplementing<ModuleWheelBase>();groundZ+=pos.z+(b==null?.3f:b.radius);
   if(pos.y>0){nf++;fy+=pos.y;fmin=Mathf.Min(fmin,pos.x);fmax=Mathf.Max(fmax,pos.x);}else{nr++;ry+=pos.y;rmin=Mathf.Min(rmin,pos.x);rmax=Mathf.Max(rmax,pos.x);}}
  carMass=mass*1000;if(nf!=2||nr!=2){analysisIssue="Roll estimate needs two wheels per axle.";return;}fy/=nf;ry/=nr;groundZ/=nf+nr;wheelbase=fy-ry;frontTrack=fmax-fmin;rearTrack=rmax-rmin;
  for(int axle=0;axle<2;axle++){bool front=axle==0;if(DraftValue(front,"suspensionKit")>0){double sag=LongTravelCore.StaticDepth(DraftLoad(front),DraftValue(front,"springRateNmm"),DraftTravel(front));double offset=LongTravelCore.Lateral(DraftTravel(front),DraftValue(front,"rideHeightMm"),sag,sag,DraftValue(front,"trackExtensionMm"));if(front)frontTrack+=2*offset;else rearTrack+=2*offset;}}
  if(wheelbase<.2||frontTrack<.2||rearTrack<.2){analysisIssue="Wheel geometry is incomplete.";return;}frontWeight=Math.Max(.1,Math.Min(.9,(cg.y-ry)/wheelbase));
  cgHeight=groundZ-cg.z+PreviewHeaveMm*.001;
  rollInput=new GarageRollCore.Input{Mass=mass,SprungMass=sprung,Height=cgHeight,RollAxis=assumedRollAxis,FrontShare=frontWeight,FrontTrack=frontTrack,RearTrack=rearTrack,FrontSpring=DraftValue(true,"springRateNmm"),RearSpring=DraftValue(false,"springRateNmm"),FrontBar=DraftValue(true,"antiRollRate"),RearBar=DraftValue(false,"antiRollRate"),Gravity=9.80665};
  rollInput.FrontTravel=DraftTravel(true);rollInput.RearTravel=DraftTravel(false);rollInput.FrontLong=DraftValue(true,"suspensionKit")>0;rollInput.RearLong=DraftValue(false,"suspensionKit")>0;rollInput.FrontBumpStart=DraftValue(true,"bumpStopStart");rollInput.RearBumpStart=DraftValue(false,"bumpStopStart");
  analysisIssue=cgHeight<=assumedRollAxis?"Assumed roll axis must be below the CG.":"";
  if(statsBody!=body){statsBody=body;ReadEngineStats();}
 }
 void ReadEngineStats(){
  var e=body.FindModuleImplementing<KDAEngine>();engineDescription=e==null?"":e.importedEngineName;gearDescription=e==null?"":e.gearboxSetupName;
  bool front=false,rear=false;foreach(var p in body.children){var m=p.FindModuleImplementing<KDAWheelMotor>();if(m==null||!m.motorEnabled||m.DriveWeight<=0)continue;if(body.transform.InverseTransformPoint(p.transform.position).y>0)front=true;else rear=true;}drive=front&&rear?"AWD":rear?"RWD":front?"FWD":"No driven wheels";
  ratingSource=null;peakKw=peakTorque=0;if(body.partInfo==null||body.partInfo.partConfig==null)return;
  foreach(var module in body.partInfo.partConfig.GetNodes("MODULE")){
   if(module.GetValue("name")!="KDAEngine")continue;var curve=module.GetNode("engineTorqueCurve");if(curve==null||e==null)continue;
   var torque=ReadRatingCurve(curve);if(torque.Count<2)continue;var source=new KerbalDriftAgency.EngineCore();source.CurveRpm=new double[torque.Count];source.CurveTorque=new double[torque.Count];int index=0;foreach(var p in torque){source.CurveRpm[index]=p.Key;source.CurveTorque[index++]=p.Value;}
   var pressure=ReadRatingCurve(module.GetNode("importedBoostCurve"));source.BoostRpm=new double[pressure.Count];source.BoostGauge=new double[pressure.Count];index=0;foreach(var p in pressure){source.BoostRpm[index]=p.Key;source.BoostGauge[index++]=p.Value;}
   // Imported specifications describe the complete source engine at its full
   // exported boost, rather than a transient throttle/SC/boost-controller cut.
   source.LimitRpm=e.referenceRpm;source.MaxBoost=KerbalDriftAgency.EngineCore.Clamp(e.maxBoostBar,0,e.importedBoostCalibration?8:2);source.ImportedPeakBoost=e.importedPeakBoostBar;
   source.BoostTorqueGain=e.boostTorqueGain;source.BoostStartRpm=e.boostStartRpm;source.BoostFullRpm=e.boostFullRpm;
   source.ImportedBoostMap=e.importedBoostCalibration&&(e.inductionSetup==0||e.inductionSetup==4||e.inductionSetup==6);
   source.ParasiticFactor=e.inductionSetup==4&&!e.importedBoostCalibration?.90:1;KerbalDriftAgency.InductionCore.Configure(source,e.inductionSetup);
   ratingSource=source;var rating=GaragePowerRating.Calculate(source);peakTorque=rating.TorqueNm;peakKw=rating.PowerKw;break;
  }
 }
 static System.Collections.Generic.SortedDictionary<double,double> ReadRatingCurve(ConfigNode curve){
  var points=new System.Collections.Generic.SortedDictionary<double,double>();if(curve==null)return points;
  foreach(string key in curve.GetValues("key")){var f=key.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);double rpm,value;if(f.Length<2||!double.TryParse(f[0],NumberStyles.Float,CultureInfo.InvariantCulture,out rpm)||!double.TryParse(f[1],NumberStyles.Float,CultureInfo.InvariantCulture,out value)||double.IsNaN(rpm)||double.IsInfinity(rpm)||double.IsNaN(value)||double.IsInfinity(value)||rpm<0||value<0)continue;points[rpm]=value;}return points;
 }
 void DrawPowerGraph(){
  if(ratingSource==null||peakKw<=0||peakTorque<=0){GUI.Label(new Rect(1100,330,310,100),"No usable imported engine curve.",label);return;}
  var r=new Rect(1134,365,244,236);float maxPower=(float)(Math.Ceiling(peakKw/50)*50),maxTorque=(float)(Math.Ceiling(peakTorque/100)*100);double end=ratingSource.LimitRpm,start=Math.Max(0,ratingSource.CurveRpm[0]);
  GUI.Label(new Rect(1100,311,314,28),"FULL-BOOST CRANK CURVES",sectionStyle);Box(r,new Color(.06f,.08f,.10f));
  for(int i=0;i<=4;i++){float y=r.yMax-r.height*i/4;Line(new Vector2(r.x,y),new Vector2(r.xMax,y),new Color(.18f,.23f,.27f),1);GUI.Label(new Rect(1093,y-10,40,22),(maxPower*i/4).ToString("0"),small);GUI.Label(new Rect(1382,y-10,54,22),(maxTorque*i/4).ToString("0"),small);}
  for(int i=0;i<=4;i++){float x=r.x+r.width*i/4;Line(new Vector2(x,r.y),new Vector2(x,r.yMax),new Color(.18f,.23f,.27f),1);GUI.Label(new Rect(x-22,r.yMax+6,55,22),((start+(end-start)*i/4)/1000).ToString("0.0"),small);}
  var old=GUI.color;GUI.color=Accent;GUI.Label(new Rect(1100,338,140,23),"POWER / kW",small);GUI.color=Orange;GUI.Label(new Rect(1276,338,140,23),"TORQUE / Nm",small);GUI.color=old;
  for(int curve=0;curve<2;curve++){Vector2 previous=Vector2.zero;for(int i=0;i<=160;i++){double rpm=start+(end-start)*i/160;var sample=GaragePowerRating.AtRpm(ratingSource,rpm);var p=new Vector2(r.x+r.width*i/160,r.yMax-(float)(curve==0?sample.PowerKw/maxPower:sample.TorqueNm/maxTorque)*r.height);if(i>0)Line(previous,p,curve==0?Accent:Orange,2);previous=p;}}
  GUI.Label(new Rect(1190,632,180,24),"ENGINE RPM × 1000",small);
  GUI.Label(new Rect(1100,674,314,28),peakKw.ToString("0")+" kW / "+(peakKw*1.341022).ToString("0")+" hp  ·  "+peakTorque.ToString("0")+" Nm",label);
  GUI.Label(new Rect(1100,718,314,92),"Full exported boost applied once. Power uses the left axis; torque uses the right. Source engine rating, before drivetrain losses; not a live dyno measurement.",small);
 }
 public bool ApplyExistingPreset(int mode){if(!open||loadingCar||view!=GarageView.Tune||PendingChanges!=0||!ValidInputs||presetBusy||mode<0||mode>3||body==null)return false;var controller=body.FindModuleImplementing<KDAController>();if(controller==null)return false;presetBusy=true;controller.chassisMode=mode;controller.ApplyPreset();StartCoroutine(FinishPreset());return true;}
 IEnumerator FinishPreset(){yield return null;yield return new WaitForEndOfFrame();if(open&&EditorLogic.fetch!=null){BindCraft();EditorLogic.fetch.SetBackup();status="Existing chassis preset applied, including its tire grip multiplier.";}presetBusy=false;}
 void DrawPresets(){
  var controller=body==null?null:body.FindModuleImplementing<KDAController>();GUI.Label(new Rect(25,110,390,25),"CHASSIS PRESET  /  suspension + tire grip",sectionStyle);
  bool old=GUI.enabled;GUI.enabled=old&&PendingChanges==0&&ValidInputs&&!presetBusy&&controller!=null;string[] names={"ROAD","SPORT","RACE","DRIFT"};for(int i=0;i<4;i++)if(Button(new Rect(25+i*103,143,98,32),names[i],controller!=null&&controller.chassisMode==i))ApplyExistingPreset(i);GUI.enabled=old;
  GUI.Label(new Rect(25,179,390,38),PendingChanges>0?"Apply or discard drafts before choosing a preset.":controller!=null&&controller.chassisMode==3?"Drift: compliant rear, mild toe-in, 1.15x grip. Driving mode unchanged.":"Applies suspension and grip immediately. Driving mode unchanged.",small);
 }
 void DrawAnalysis(){
  RefreshAnalysis();if(analysisIssue.Length>0){GUI.Label(new Rect(1094,230,310,110),analysisIssue,label);return;}
  GUI.Label(new Rect(1094,225,310,28),"STEADY CORNERING ESTIMATE",sectionStyle);
  var one=GarageRollCore.Estimate(rollInput,1);float ymax=(float)Math.Max(.5,one.Valid?one.Degrees*1.7:2);var r=new Rect(1094,296,308,188);Axes(r,ymax,"Body roll (degrees)","0                    lateral acceleration                    1.5 g");
  bool have=false;Vector2 previous=Vector2.zero;for(int i=0;i<=75;i++){double g=i*.02;var p=GarageRollCore.Estimate(rollInput,g);if(!p.Valid){have=false;continue;}var point=new Vector2(r.x+(float)(g/1.5)*r.width,r.yMax-(float)p.Degrees/ymax*r.height);if(have)Line(previous,point,Accent,2);previous=point;have=true;}
  GUI.Label(new Rect(1094,517,310,28),one.Valid?"At 1 g: "+one.Degrees.ToString("0.00")+" deg":"At 1 g: outside model limits",label);
  GUI.Label(new Rect(1094,550,310,28),"CG above contact plane: "+cgHeight.ToString("0.000")+" m",small);
  GUI.Label(new Rect(1094,582,310,26),"Assumed roll-axis height: "+assumedRollAxis.ToString("0.00")+" m",small);
  assumedRollAxis=GUI.HorizontalSlider(new Rect(1094,617,308,18),assumedRollAxis,0,(float)Math.Max(0,Math.Min(1,cgHeight-.02)));
  GUI.Label(new Rect(1094,647,310,157),"Kerbin gravity, level ground, direct wheel rates. Uses draft springs and capped anti-roll model. Roll-axis height is an analysis assumption, not an imported value. Excludes aero, tire compliance, transient damping and geometric load transfer. Curve stops when its loaded-wheel/travel assumptions fail; it does not predict rollover.",small);
 }
}
