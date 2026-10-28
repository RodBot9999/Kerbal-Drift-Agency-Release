using UnityEngine;

// Imported cefaero reference forces, in N; KSP rigidbody force units are kN.
public class KDAAero : PartModule {
 [KSPField(isPersistant=true)] public float frontDownforceN,rearDownforceN,constantFrontN,constantRearN,dragArea;
 [KSPField(isPersistant=true)] public float referenceSpeedKmh=200,referenceDensity=1.2041f,sideDrag=1.25f,verticalDrag=1.5f;
 [KSPField(isPersistant=true)] public Vector3 frontPoint,rearPoint;
 [KSPField(guiActive=true,guiName="Aero downforce",guiUnits=" N",guiFormat="F0")] public float downforceN;
 void FixedUpdate(){
  if(!HighLogic.LoadedSceneIsFlight||vessel==null||vessel.packed||part.rb==null)return;
  Vector3 velocity=(Vector3)vessel.srf_velocity;
  Vector3 local=part.transform.InverseTransformDirection(velocity);
  float rho=Mathf.Max(0,(float)vessel.atmDensity),forward=local.y;
  float reference=Mathf.Max(1,referenceSpeedKmh/3.6f);
  float factor=rho/Mathf.Max(.01f,referenceDensity)*forward*forward/(reference*reference);
  // Reduce downforce when travelling backwards or the car is overturned.
  Vector3 up=-part.transform.forward;
  float attitude=Mathf.Clamp01(Vector3.Dot(up,(Vector3)vessel.upAxis));
  factor*=forward>0?attitude:0;
  float f=frontDownforceN*factor+constantFrontN*attitude,r=rearDownforceN*factor+constantRearN*attitude;
  downforceN=f+r;
  part.rb.AddForceAtPosition(-up*(f*.001f),part.transform.TransformPoint(frontPoint));
  part.rb.AddForceAtPosition(-up*(r*.001f),part.transform.TransformPoint(rearPoint));
  Vector3 drag=new Vector3(local.x*Mathf.Abs(local.x)*sideDrag,local.y*Mathf.Abs(local.y),local.z*Mathf.Abs(local.z)*verticalDrag);
  part.rb.AddForce(part.transform.TransformDirection(-drag)*(.0005f*rho*dragArea));
 }
 public override string GetInfo(){return "Imported aerodynamic balance and drag. Downforce scales with speed squared and air density.";}
}
