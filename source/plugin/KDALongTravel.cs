using System;using UnityEngine;
public partial class KDASuspension {
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Suspension hardware")]
 [UI_ChooseOption(options=new[]{"Standard 160","Rally","Desert"})] public int suspensionKit=0;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Wheel travel (mm)",guiFormat="F0")]
 [UI_FloatRange(minValue=200,maxValue=700,stepIncrement=10)] public float travelMm=300;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Sag ceiling for spring calculator (%)",guiFormat="F0")]
 [UI_FloatRange(minValue=20,maxValue=55,stepIncrement=1)] public float targetSagPercent=35;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Bump stop starts at (%)",guiFormat="F0")]
 [UI_FloatRange(minValue=60,maxValue=95,stepIncrement=1)] public float bumpStopStart=80;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Bump stop strength",guiFormat="F2")]
 [UI_FloatRange(minValue=0,maxValue=6,stepIncrement=.1f)] public float bumpStopStrength=3;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Position damping gain",guiFormat="F2")]
 [UI_FloatRange(minValue=0,maxValue=3,stepIncrement=.1f)] public float bypassDamping=1;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Droop damping gain",guiFormat="F2")]
 [UI_FloatRange(minValue=0,maxValue=3,stepIncrement=.1f)] public float droopDamping=1;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Track extension per side (mm)",guiFormat="F0")]
 [UI_FloatRange(minValue=0,maxValue=350,stepIncrement=10)] public float trackExtensionMm=0;
 int observedKit;float lastDownSpeed;readonly SuspensionTravelMonitor travelMonitor=new SuspensionTravelMonitor();
 public bool LongTravel {get{return suspensionKit!=0;}}public float Travel {get{return (float)LongTravelCore.Travel(suspensionKit,travelMm);}}
 public float EffectiveRate {get;private set;}public float BumpStopForce {get;private set;}public float LateralOffset {get;private set;}public float StaticSag {get;private set;}
 public SuspensionTravelMonitor TravelMonitor {get{return travelMonitor;}}
 public void AcceptGarageSetup(){suspensionKit=Mathf.Clamp(suspensionKit,0,2);observedKit=suspensionKit;RefreshTravelControls();}
 void RefreshTravelControls(){
  var spring=Fields["springRateNmm"].uiControlEditor as UI_FloatRange;var height=Fields["rideHeightMm"].uiControlEditor as UI_FloatRange;
  if(spring!=null){spring.minValue=LongTravel?5:93.75f;spring.stepIncrement=LongTravel?1:2.5f;}if(height!=null){height.maxValue=LongTravel?350:30;height.stepIncrement=LongTravel?5:2;}
  foreach(string name in new[]{"travelMm","targetSagPercent","bumpStopStart","bumpStopStrength","bypassDamping","droopDamping","trackExtensionMm"})Fields[name].guiActiveEditor=LongTravel;
  Events["SuggestSpring"].guiActiveEditor=LongTravel;
 }
 public float EditorCornerLoad(){
  if(Body==null||EditorLogic.fetch==null||EditorLogic.fetch.ship==null)return 2.5f;float total=0,weighted=0,frontY=float.MinValue,rearY=float.MaxValue;int nf=0,nr=0;
  foreach(var p in EditorLogic.fetch.ship.parts){float mass=Mathf.Max(0,p.mass+(float)p.GetResourceMass());total+=mass;weighted+=mass*Body.transform.InverseTransformPoint(p.transform.TransformPoint(p.CoMOffset)).y;}
  foreach(var p in Body.children)if(p.FindModuleImplementing<KDASuspension>()!=null){float y=Body.transform.InverseTransformPoint(p.transform.position).y;if(y>0){frontY=Mathf.Max(frontY,y);nf++;}else{rearY=Mathf.Min(rearY,y);nr++;}}
  float share=total>0&&frontY>rearY?Mathf.Clamp((weighted/total-rearY)/(frontY-rearY),.1f,.9f):.5f;
  return total*9.80665f*(Front?share:1-share)/Mathf.Max(1,Front?nf:nr);
 }
 [KSPEvent(guiActiveEditor=true,guiName="Calculate rough-ground spring")]
 public void SuggestSpring(){if(!HighLogic.LoadedSceneIsEditor||!LongTravel)return;springRateNmm=(float)LongTravelCore.SuggestedSpring(EditorCornerLoad(),Travel,targetSagPercent);}
 public void SelectHardware(int mode){
  if(!HighLogic.LoadedSceneIsEditor)return;suspensionKit=Mathf.Clamp(mode,0,2);
  if(suspensionKit==0){var sc=Body==null?null:Body.FindModuleImplementing<KDAController>();var p=KDACore.Preset(sc==null?1:sc.chassisMode);springRateNmm=(float)(Front?p.FrontSpring:p.RearSpring);rideHeightMm=(float)p.Height;antiRollRate=(float)(Front?p.FrontBar:p.RearBar);bumpDamping=(float)p.Bump;reboundDamping=(float)p.Rebound;fastBumpDamping=(float)p.FastBump;fastReboundDamping=(float)p.FastRebound;}
  else{bool desert=suspensionKit==2;travelMm=desert?550:300;rideHeightMm=desert?180:80;targetSagPercent=desert?40:35;trackExtensionMm=desert?150:0;bumpStopStart=desert?75:80;bumpStopStrength=desert?3.5f:3;bypassDamping=desert?1.7f:1;droopDamping=1;antiRollRate=Front?(desert?3:5):(desert?2:3);bumpDamping=.65f;reboundDamping=.9f;fastBumpDamping=.7f;fastReboundDamping=.8f;SuggestSpring();}
  suspensionVersion=2;AcceptGarageSetup();
 }
 void ObserveTravel(float dt,bool grounded,float depth,float downSpeed){travelMonitor.Step(dt,grounded,depth/Travel,lastDownSpeed);lastDownSpeed=downSpeed;}
}
