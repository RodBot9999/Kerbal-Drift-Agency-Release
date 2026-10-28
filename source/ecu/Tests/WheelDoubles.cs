// Isolated test doubles, never built into the ECU or game runtime.
using System;using System.Collections.Generic;using System.Reflection;
namespace UnityEngine {
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 down {get{return new Vector3(0,-1,0);}}public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}public static Vector3 operator *(Vector3 a,float b){return new Vector3(a.x*b,a.y*b,a.z*b);}}
 public static class Mathf {public static float Max(float a,float b){return Math.Max(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}public static float Abs(float a){return Math.Abs(a);}public static float Clamp(float a,float lo,float hi){return Math.Max(lo,Math.Min(hi,a));}public static float Clamp01(float a){return Math.Max(0,Math.Min(1,a));}}
 public enum KeyCode {Space}
 public static class Input {public static bool Pressed;public static bool GetKey(KeyCode key){return Pressed;}}
 public static class Time {public static float fixedDeltaTime=.02f;}
}
namespace VehiclePhysics {
 public class TireFriction {public enum Model {Flat,Smooth}public class Settings {public UnityEngine.Vector2 adherent,peak,limit;}public Model model;public Settings settings=new Settings();public void SetupFrictionCurves(){}}
 public class VPWheelCollider {public UnityEngine.Vector3 center;public float suspensionDistance=.2f,runtimeSuspensionTravel=.2f,suspensionOffset=.01f,springRate=20,runtimeSpringRate=20,damperRate=2,runtimeDamperRate=2;}
}
public class ConfigNode {readonly Dictionary<string,string> data=new Dictionary<string,string>();public bool HasValue(string key){return data.ContainsKey(key);}public string GetValue(string key){string value;return data.TryGetValue(key,out value)?value:null;}public void SetValue(string key,string value,bool create){data[key]=value;}}
public class PartModule {public Part part;public void Save(ConfigNode node){foreach(var f in GetType().GetFields())if(f.FieldType==typeof(bool)||f.FieldType==typeof(float))node.SetValue(f.Name,Convert.ToString(f.GetValue(this),System.Globalization.CultureInfo.InvariantCulture),true);EcuWheel w;if(EcuHooks.Wheels.TryGetValue(part.FindModuleImplementing<ModuleWheelBase>().Wheel,out w))w.SaveNative(this,node);}}
public class Part {public Vessel vessel;public UnityEngine.Vector3 position;public readonly List<PartModule> Modules=new List<PartModule>();public T FindModuleImplementing<T>() where T:PartModule {foreach(var m in Modules)if(m is T)return (T)m;return null;}public T Add<T>() where T:PartModule,new(){var m=new T();m.part=this;Modules.Add(m);return m;}}
public class Vessel {public bool packed;}
public static class FlightGlobals {public static Vessel ActiveVessel;}
public class KSPWheelController {
 public class State {public UnityEngine.Vector2 localWheelVelocity;public float angularVelocity,contactSpeed;}
 public bool IsGrounded=true,keyboardDrive,keyboardBrakes,keyboardSteering=true;public float steeringResponse=2,Orientation=1;public float maxSteerAngle=25;public float WheelRadius=.3f,maxDriveTorque=.12f,maxRpm=900,driveInput=.8f,driveState=.8f,steerInput=.3f,steerState=.3f,brakeInput=.7f,brakeState=.7f,brakeResponse=2;
 public State currentState=new State();public VehiclePhysics.VPWheelCollider wheelCollider=new VehiclePhysics.VPWheelCollider();public VehiclePhysics.TireFriction tireFriction=new VehiclePhysics.TireFriction();
}
public class ModuleWheelBase:PartModule {public ModuleWheels.WheelSubsystems InopSystems=new ModuleWheels.WheelSubsystems();public KSPWheelController Wheel=new KSPWheelController();public bool autoFriction=true;public float frictionAdherent=.3f,frictionPeak=1.2f,frictionLimit=1,frictionMultiplier=1.4f,adherentStart=.5f,peakStart=2,limitStart=5;}
namespace ModuleWheels {
 public class WheelSubsystem { [Flags] public enum SystemTypes {Motor=1,WheelCollider=2,Tire=4,Brakes=8} }
 public class WheelSubsystems {public WheelSubsystem.SystemTypes Failed;public bool HasType(WheelSubsystem.SystemTypes flags){return (Failed&flags)!=0;}}
 public class ModuleWheelBrakes:PartModule {}
 public class ModuleWheelSteering:PartModule {public bool steeringEnabled=true,autoSteeringAdjust=true;public float angleTweakable=25,steeringRange=25,responseTweakable=1,steeringInput=.3f;}
 public class ModuleWheelMotor:PartModule {public bool motorEnabled=true,motorInverted;protected float GetMotorOrientationSign(){return 1;}}
 public class ModuleWheelSuspension:PartModule {public bool autoSpringDamper=true;public float suspensionDistance=.2f;}
}
public class KDAECU {
 public sealed class ChoiceValue {public int Group;}
 public bool Active=true,ecuEnabled=true,adaptiveSteering=true,brakeTraction=true,yawStability=true,absEnabled=true;
 public int addonProfile;public float TestAddonScale=1;internal void PrepareAddonPhysics(){}internal float AddonBrakeAllowance(EcuWheel w){return IsFront(w)?TestAddonScale:1;}
 public float YawError,SteeringCentreX,SteeringDegrees;internal float SteeringOrientation(EcuWheel w){return w.Wheel.Orientation;}public Vessel vessel;
 readonly Dictionary<Part,ChoiceValue> choices=new Dictionary<Part,ChoiceValue>();
 public ChoiceValue Choice(Part p){ChoiceValue c;if(!choices.TryGetValue(p,out c)){c=new ChoiceValue();choices[p]=c;}return c;}
 internal bool IsFront(EcuWheel w){return Choice(w.Part).Group==1;}
 public UnityEngine.Vector3 Local(Part p){return p.position;}
}
internal static class EcuHooks {internal static readonly Dictionary<KSPWheelController,EcuWheel> Wheels=new Dictionary<KSPWheelController,EcuWheel>();internal static void Register(EcuWheel w){Wheels[w.Wheel]=w;}internal static void Unregister(EcuWheel w){Wheels.Remove(w.Wheel);}}
