using System;
using ModuleWheels;
using UnityEngine;

public partial class KDASuspension : ModuleWheelSuspension {
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Spring rate (N/mm)",guiFormat="F0")]
 [UI_FloatRange(minValue=93.75f,maxValue=280,stepIncrement=2.5f)] public float springRateNmm=65;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Ride height offset (mm)",guiFormat="F0")]
 [UI_FloatRange(minValue=-20,maxValue=30,stepIncrement=2)] public float rideHeightMm=0;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Bump damping",guiFormat="F2")]
 [UI_FloatRange(minValue=.30f,maxValue=2.1f,stepIncrement=.025f)] public float bumpDamping=.45f;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Rebound damping",guiFormat="F2")]
 [UI_FloatRange(minValue=.45f,maxValue=2.6f,stepIncrement=.025f)] public float reboundDamping=.70f;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Fast bump damping",guiFormat="F2")]
 [UI_FloatRange(minValue=.25f,maxValue=2.1f,stepIncrement=.025f)] public float fastBumpDamping=.30f;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Fast rebound damping",guiFormat="F2")]
 [UI_FloatRange(minValue=.375f,maxValue=2.6f,stepIncrement=.025f)] public float fastReboundDamping=.50f;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Anti-roll rate (N/mm)",guiFormat="F0")]
 [UI_FloatRange(minValue=0,maxValue=120,stepIncrement=1)] public float antiRollRate=12;
 [KSPField(isPersistant=true)] public int suspensionVersion;
 [KSPField(isPersistant=true,guiActiveEditor=true,guiName="Toe (deg/wheel)",guiFormat="F2")]
 [UI_FloatRange(minValue=-.5f,maxValue=.5f,stepIncrement=.05f)] public float toeIn=.15f;
 [KSPField(isPersistant=true)] public bool axleInitialized;
 public float AppliedAntiRoll {get;private set;}
 public float AppliedDamper {get;private set;}
 // Only the left axle owner applies the force. Its observation is read by both
 // telemetry wheel rows so right-wheel update order cannot erase the result.
 public float BarSampleTime {get;private set;}
 public bool BarComputed {get;private set;}
 public bool BarForceApplied {get;private set;}
 public float BarRequested {get;private set;}
 public float BarForceCap {get;private set;}
 public float BarCapped {get;private set;}
 public string BarGate {get;private set;}
 public uint BarPartnerId {get;private set;}
 public bool Front {get{return Body!=null&&Body.transform.InverseTransformPoint(part.transform.position).y>0;}}
 float supportedMass,originalVisualOffset;Vector3 originalCenter,baseCenter;bool calibrated;
 public float BarPreload {get;private set;}
 public float PhysicalCompression {get;private set;}
 public float SupportedMass {get{return supportedMass;}}
 float physicalDepth;
 KDASuspensionBar barRunner;
 float targetVisualHeight,visualHeight;bool visualReady;
 Part Body {get{return part.parent!=null&&part.parent.FindModuleImplementing<KDAEngine>()!=null?part.parent:null;}}
 public override void OnStart(StartState state){
  base.OnStart(state);
  if(!LongTravel&&suspensionVersion<1&&(HighLogic.LoadedSceneIsEditor||HighLogic.LoadedSceneIsFlight)){
   springRateNmm=Mathf.Clamp(springRateNmm*1.5f,75,210);antiRollRate=Mathf.Clamp(antiRollRate*1.5f,0,90);
   bumpDamping=Mathf.Clamp(bumpDamping*1.5f,.30f,1.8f);reboundDamping=Mathf.Clamp(reboundDamping*1.5f,.45f,2.25f);
   fastBumpDamping=Mathf.Clamp(fastBumpDamping*1.5f,.25f,1.8f);fastReboundDamping=Mathf.Clamp(fastReboundDamping*1.5f,.375f,2.25f);suspensionVersion=1;
  }
  if(!LongTravel&&suspensionVersion<2&&(HighLogic.LoadedSceneIsEditor||HighLogic.LoadedSceneIsFlight)){
   var chassis=Body==null?null:Body.FindModuleImplementing<KDAController>();
   float factor=chassis!=null&&chassis.chassisMode==2?1.30f:1.25f;
   float damping=Mathf.Sqrt(factor);
   springRateNmm=Mathf.Clamp(springRateNmm*factor,93.75f,280);antiRollRate=Mathf.Clamp(antiRollRate*factor,0,120);
   bumpDamping=Mathf.Clamp(bumpDamping*damping,.30f,2.1f);reboundDamping=Mathf.Clamp(reboundDamping*damping,.45f,2.6f);
   fastBumpDamping=Mathf.Clamp(fastBumpDamping*damping,.25f,2.1f);fastReboundDamping=Mathf.Clamp(fastReboundDamping*damping,.375f,2.6f);suspensionVersion=2;
  }
  foreach(string name in new[]{"springTweakable","damperTweakable","autoSpringDamper","autoBoost","useAutoBoost"}){var f=Fields[name];if(f!=null){f.guiActive=false;f.guiActiveEditor=false;}}
  var evt=Events["EvtAutoSpringDamperToggle"];if(evt!=null){evt.guiActive=false;evt.guiActiveEditor=false;}
  AcceptGarageSetup();if(HighLogic.LoadedSceneIsFlight){barRunner=gameObject.AddComponent<KDASuspensionBar>();barRunner.Owner=this;}
 }
 void Update(){
  if(!HighLogic.LoadedSceneIsEditor||Body==null)return;
  if(observedKit!=suspensionKit)SelectHardware(suspensionKit);RefreshTravelControls();
  Fields["toeIn"].guiName=Front?"Front toe (deg/wheel)":"Rear toe (deg/wheel)";
  if(axleInitialized)return;
  toeIn=Front?0:.15f;antiRollRate=Front?26.25f:18.75f;
  var steering=part.FindModuleImplementing<ModuleWheelSteering>();if(steering!=null)steering.steeringEnabled=Front;
  axleInitialized=true;
 }
 [KSPEvent(guiActiveEditor=true,guiName="Reset suspension setup")]
 public void ResetSetup(){springRateNmm=121.875f;rideHeightMm=0;bumpDamping=.675f*Mathf.Sqrt(1.25f);reboundDamping=1.05f*Mathf.Sqrt(1.25f);fastBumpDamping=.45f*Mathf.Sqrt(1.25f);fastReboundDamping=.75f*Mathf.Sqrt(1.25f);antiRollRate=Front?26.25f:18.75f;toeIn=Front?0:.15f;suspensionVersion=2;}
 // Own the Unity message: the stock private FixedUpdate would overwrite our rates.
 void FixedUpdate(){
  if(!HighLogic.LoadedSceneIsFlight||!baseSetup||part.packed||wheelBase==null||wheelBase.Wheel==null)return;
  var w=wheelBase.Wheel;var col=w.wheelCollider;if(col==null)return;
  float rate=LongTravel?Mathf.Clamp(springRateNmm,5,280):Mathf.Clamp(springRateNmm,93.75f,280),travel=Travel;
  if(Body==null)return;
  if(!calibrated){originalCenter=col.center;originalVisualOffset=col.suspensionOffset;calibrated=true;}
  {
   // Fuel and payload can change after launch; use current native mass/CG.
   float frontY=float.MinValue,rearY=float.MaxValue;int frontCount=0,rearCount=0;
   if(Body==null)return;
   foreach(Part p in Body.children)if(p.FindModuleImplementing<KDASuspension>()!=null){float y=Body.transform.InverseTransformPoint(p.transform.position).y;if(y>0){frontY=Mathf.Max(frontY,y);frontCount++;}else{rearY=Mathf.Min(rearY,y);rearCount++;}}
   float comY=Body.transform.InverseTransformPoint((Vector3)vessel.CoM).y;
   float frontShare=frontY>rearY?Mathf.Clamp((comY-rearY)/(frontY-rearY),.1f,.9f):.5f;
   supportedMass=Mathf.Max(.01f,(float)vessel.totalMass*(Front?frontShare:1-frontShare)/Mathf.Max(1,Front?frontCount:rearCount));

  }
  float mass=supportedMass;
  // Longer travel retains the mounting reference. The collider's own sprungMass
  // is the wheel, so compensate the reference using calibrated supported weight.
  col.suspensionDistance=travel;col.runtimeSuspensionTravel=travel;
  col.suspensionAnchor=.25f;
  float gravity=(float)FlightGlobals.getGeeForceAtPosition(part.transform.position).magnitude;
  float reference=LongTravel?(float)LongTravelCore.Reference(LongTravelCore.Height(suspensionKit,rideHeightMm),mass*gravity,rate,travel):(float)SuspensionCore.ReferenceOffset(Mathf.Clamp(rideHeightMm,-20,30),mass*gravity,rate);
  // VP's contactDepth omits the adjustable collider.center reference. Keep
  // the legacy bar behavior for Standard; long-travel limits need the actual
  // stroke measured from the shifted full-droop position.
  float actualDepth=LongTravel&&w.IsGrounded?Mathf.Clamp(w.currentState.contactDepth-col.center.y,0,travel):0;
  physicalDepth=w.IsGrounded?Mathf.Clamp((LongTravel?actualDepth:w.currentState.contactDepth)-BarPreload,0,travel):0;
  PhysicalCompression=LongTravel?actualDepth:physicalDepth;
  baseCenter=originalCenter+Vector3.down*reference;
  StaticSag=(float)LongTravelCore.StaticDepth(mass*gravity,rate,travel);
  LateralOffset=LongTravel?(float)LongTravelCore.Lateral(travel,rideHeightMm,StaticSag,physicalDepth,trackExtensionMm):0;
  if(LongTravel){float side=Mathf.Sign(Body.transform.InverseTransformPoint(part.transform.position).x);baseCenter+=col.cachedTransform.InverseTransformDirection(Body.transform.right*(side*LateralOffset));}
  col.center=baseCenter;
  // VP's visual suspension position excludes collider.center. Match its visual
  // offset to the collider displacement so the mesh stays at the contact patch.
  col.suspensionOffset=originalVisualOffset+reference;
  // Native cosmetic raycasts can hit this car's own fenders. Ground contact
  // from the physics wheel is authoritative; keep rotation animation native.
  col.updateSuspension=false;
  if(w.IsGrounded)targetVisualHeight=Vector3.Dot(w.currentState.hit.point-col.cachedTransform.position,col.cachedTransform.up)+w.WheelRadius;
  else targetVisualHeight=col.center.y-travel*.25f;
  if(LongTravel)targetVisualHeight=Mathf.Clamp(targetVisualHeight,col.center.y-travel*.25f,col.center.y+travel*.75f);
  if(!visualReady){visualHeight=targetVisualHeight;visualReady=true;}
  BumpStopForce=LongTravel?(float)LongTravelCore.BumpForce(rate,travel,actualDepth,bumpStopStart,bumpStopStrength):0;
  EffectiveRate=LongTravel?(float)LongTravelCore.EffectiveRate(rate,travel,actualDepth,BumpStopForce):rate;
  col.springRate=EffectiveRate;col.runtimeSpringRate=EffectiveRate;
  AppliedDamper=(float)SuspensionCore.Damper(rate,mass,w.currentState.contactSpeed,
   Mathf.Clamp(bumpDamping,.30f,2.1f),Mathf.Clamp(reboundDamping,.45f,2.6f),Mathf.Clamp(fastBumpDamping,.25f,2.1f),Mathf.Clamp(fastReboundDamping,.375f,2.6f));
  if(LongTravel)AppliedDamper*=(float)LongTravelCore.DampingGain(travel,actualDepth,w.currentState.contactSpeed,bypassDamping,droopDamping);
  col.damperRate=AppliedDamper;col.runtimeDamperRate=AppliedDamper;
  suspCompression=w.currentState.suspensionCompression;suspMass=mass;
  ObserveTravel(Time.fixedDeltaTime,w.IsGrounded,PhysicalCompression,Body.rb==null?0:Vector3.Dot(Body.rb.GetPointVelocity(part.transform.position),Body.transform.forward));
  if(Mathf.Abs(vessel.ctrlState.wheelSteer+vessel.ctrlState.wheelSteerTrim)<.002f)
   w.steerState=Mathf.MoveTowards(w.steerState,0,Time.fixedDeltaTime*6);

 }
 // The steering and wheel pivots are children of this suspension pivot. Only
 // translate it along the suspension axis; never rotate the launched part.
 new void LateUpdate(){
  if(!HighLogic.LoadedSceneIsFlight||!visualReady||part.packed||wheelBase==null||wheelBase.Wheel==null)return;
  var col=wheelBase.Wheel.wheelCollider;if(col==null||col.suspensionTransform==null)return;
  visualHeight=Mathf.Lerp(visualHeight,targetVisualHeight,1-Mathf.Exp(-Time.deltaTime*40));
  col.suspensionTransform.position=col.cachedTransform.position+col.cachedTransform.up*visualHeight;
  if(LongTravel)col.suspensionTransform.position+=Vector3.ProjectOnPlane(col.cachedTransform.TransformVector(col.center),col.cachedTransform.up);
  suspensionPos=col.suspensionTransform.localPosition;
 }
 // Run after all per-wheel modules established their base spring references.
 public void ApplyBar(){
  if(!HighLogic.LoadedSceneIsFlight||part.packed||Body==null||wheelBase==null||wheelBase.Wheel==null||!calibrated)return;
  if(Body.transform.InverseTransformPoint(part.transform.position).x>=0)return;
  BarSampleTime=Time.fixedTime;BarComputed=BarForceApplied=false;BarRequested=BarForceCap=BarCapped=0;BarPartnerId=0;
  BarGate="no_partner";AppliedAntiRoll=0;SetBarPreload(0);
  var left=wheelBase.Wheel;
  foreach(Part p in Body.children){
   if(p==part||Body.transform.InverseTransformPoint(p.transform.position).x<=0)continue;
   var other=p.FindModuleImplementing<KDASuspension>();
   if(other==null||other.Front!=Front||!other.calibrated||other.wheelBase==null||other.wheelBase.Wheel==null)continue;
   var right=other.wheelBase.Wheel;BarPartnerId=p.flightID;other.AppliedAntiRoll=0;other.SetBarPreload(0);
   if(!left.IsGrounded&&!right.IsGrounded){BarGate="both_airborne";return;}
   float ls=(float)LongTravelCore.Rate(suspensionKit,springRateNmm),rs=(float)LongTravelCore.Rate(other.suspensionKit,other.springRateNmm),travel=Mathf.Min(Travel,other.Travel);
   BarRequested=(physicalDepth-other.physicalDepth)*Mathf.Clamp(antiRollRate,0,120);
   BarForceCap=LongTravel||other.LongTravel?(float)LongTravelCore.BarCap(ls,rs,travel,supportedMass*9.80665,other.supportedMass*9.80665):(float)SuspensionCore.BarCapacity(ls,rs,.16);
   float force=(float)SuspensionCore.CoupledBar(physicalDepth,other.physicalDepth,antiRollRate,ls,rs,travel);force=Mathf.Clamp(force,-BarForceCap,BarForceCap);
   BarComputed=BarForceApplied=true;BarCapped=AppliedAntiRoll=force;other.AppliedAntiRoll=-force;
   SetBarPreload(force/(LongTravel?Mathf.Max(ls,EffectiveRate):ls));other.SetBarPreload(-force/(other.LongTravel?Mathf.Max(rs,other.EffectiveRate):rs));
   BarGate=left.IsGrounded&&right.IsGrounded?"spring_coupled":"one_contact_spring_coupled";
   return;
  }
 }
 void SetBarPreload(float offset){
  BarPreload=offset;var col=wheelBase.Wheel.wheelCollider;
  col.center=baseCenter+Vector3.down*offset;
  col.suspensionOffset=originalVisualOffset+(originalCenter.y-baseCenter.y)+offset;
 }
 public override void OnDestroy(){if(barRunner!=null)Destroy(barRunner);base.OnDestroy();}
 public override string GetInfo(){return "Axle-linked coilovers: independent spring rate and ride height, bump/rebound damping, fast-shaft damping, anti-roll and toe-in. Tune in SPH/VAB.";}
}

// Native wheel springs supply the forces and their tire loads together. No
// additional rigidbody torque or artificial tire downforce is introduced.
[DefaultExecutionOrder(1100)]
public sealed class KDASuspensionBar:MonoBehaviour {
 public KDASuspension Owner;
 void FixedUpdate(){if(Owner!=null)Owner.ApplyBar();}
}
