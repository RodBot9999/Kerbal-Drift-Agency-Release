using UnityEngine;

// Render-camera offset only. Native IVA owns eye pose, look rotation and zoom.
[DefaultExecutionOrder(2000)]
public sealed class KDADriverCamera:MonoBehaviour {
 public KDAHud Hud;
 public Vector3 AppliedBodyOffset {get;private set;}
 public bool Applied {get;private set;}
 public InternalCamera DriverCamera {get{return owned;}}
 InternalCamera owned;
 static void SyncWorld(InternalCamera camera){if(FlightCamera.fetch!=null)FlightCamera.fetch.transform.position=InternalSpace.InternalToWorld(camera.transform.position);}
 void LateUpdate(){
  Applied=false;AppliedBodyOffset=Vector3.zero;var manager=CameraManager.Instance;var crew=manager==null?null:manager.IVACameraActiveKerbal;
  bool active=HighLogic.LoadedSceneIsFlight&&Hud!=null&&Hud.vessel!=null&&manager!=null&&manager.currentCameraMode==CameraManager.CameraMode.IVA&&crew!=null&&crew.InPart!=null&&crew.InPart.vessel==Hud.vessel;
  if(!active){owned=null;return;}var camera=InternalCamera.Instance;if(camera==null)return;
  // KSP 1.12.5 uses InternalCamera for crew IVA. Its public UpdateState
  // restores the native eye-relative position, including look/TrackIR state.
  // Rebuild that base each frame so this offset never accumulates.
  camera.UpdateState();owned=camera;Hud.SetCameraPosition(Hud.cameraSide,Hud.cameraForward,Hud.cameraHeight);
  var offset=Hud.CameraPositionBody;camera.transform.position+=InternalSpace.WorldToInternal(Hud.part.transform.rotation)*offset;SyncWorld(camera);AppliedBodyOffset=offset;Applied=true;
 }
 void OnDisable(){
  var manager=CameraManager.Instance;var crew=manager==null?null:manager.IVACameraActiveKerbal;
  if(owned!=null&&Hud!=null&&manager!=null&&manager.currentCameraMode==CameraManager.CameraMode.IVA&&crew!=null&&crew.InPart!=null&&crew.InPart.vessel==Hud.vessel){owned.UpdateState();SyncWorld(owned);}owned=null;Applied=false;
 }
}
