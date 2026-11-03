using System;using UnityEngine;using KSP.UI.Screens;

[KSPAddon(KSPAddon.Startup.MainMenu,true)]
public sealed class KDAGarageLauncher:MonoBehaviour {
 static KDAGarageLauncher instance;
 const string LockName="KDA.Garage.Launcher";
 ApplicationLauncherButton button;ApplicationLauncher owner;Texture2D icon;bool menu,registering,sceneChanging;
 bool Supported {get{return HighLogic.LoadedSceneIsEditor||HighLogic.LoadedScene==GameScenes.SPACECENTER;}}
 void Awake(){if(instance!=null&&instance!=this){Destroy(gameObject);return;}instance=this;DontDestroyOnLoad(gameObject);}
 void Start(){if(instance!=this)return;GameEvents.onGUIApplicationLauncherReady.Add(AddButton);GameEvents.onGameSceneLoadRequested.Add(SceneChanging);GameEvents.onLevelWasLoadedGUIReady.Add(SceneReady);AddButton();}
 void SceneChanging(GameScenes scene){sceneChanging=true;Dismiss();RemoveButton();}
 void SceneReady(GameScenes scene){sceneChanging=false;AddButton();}
 void RemoveButton(){if(button!=null&&owner!=null)owner.RemoveModApplication(button);button=null;owner=null;}
 void AddButton(){
  if(instance!=this||registering||sceneChanging||!Supported||button!=null||ApplicationLauncher.Instance==null||!ApplicationLauncher.Ready)return;
  registering=true;try{
   if(icon==null){icon=KDAHudLauncher.CreateIcon();icon.name="KDA garage toolbar";}
   owner=ApplicationLauncher.Instance;button=owner.AddModApplication(Show,Hide,null,null,null,null,ApplicationLauncher.AppScenes.SPH|ApplicationLauncher.AppScenes.VAB|ApplicationLauncher.AppScenes.SPACECENTER,icon);
  }finally{registering=false;}
 }
 void Show(){menu=true;if(KDAWorldGarage.Instance!=null)try{KDAWorldGarage.Instance.RefreshLocations();}catch(Exception ex){Debug.LogWarning("[KDA Garage] Discovery: "+ex.Message);}UpdateLock();}
 void Hide(){menu=false;InputLockManager.RemoveControlLock(LockName);}
 void Dismiss(){Hide();if(button!=null)button.SetFalse(false);}
 void UpdateLock(){if(!menu)return;if(HighLogic.LoadedSceneIsEditor&&EditorLogic.fetch!=null)EditorLogic.fetch.Lock(true,true,true,LockName);else InputLockManager.SetControlLock(ControlTypes.KSC_FACILITIES,LockName);}
 void Update(){if(!Supported){Dismiss();return;}AddButton();if(KDAGarage.Instance!=null&&KDAGarage.Instance.IsOpen)Dismiss();else UpdateLock();}
 void OnGUI(){
  if(!menu||!Supported)return;var old=GUI.skin;GUI.skin=HighLogic.Skin;
  var rect=new Rect(Mathf.Max(8,Screen.width-314),Mathf.Max(8,Screen.height-232),294,160);
  GUI.Box(rect,GUIContent.none);var title=new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};GUI.Label(new Rect(rect.x+14,rect.y+5,266,23),"KDA GARAGE",title);var description=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=12};var world=KDAWorldGarage.Instance;
  GUI.Label(new Rect(rect.x+14,rect.y+30,266,49),world!=null&&world.Locations.Count>0?"Open the placed garage workshop. Your current editor car is kept.":"Open the garage overlay in the SPH. A placed KK garage is optional.",description);
  if(GUI.Button(new Rect(rect.x+14,rect.y+84,266,36),"GO TO KDA GARAGE")){Dismiss();if(world!=null)world.OpenFromToolbar();else if(KDAGarage.Instance!=null)KDAGarage.Instance.Open();}
  if(GUI.Button(new Rect(rect.x+14,rect.y+126,266,25),"CLOSE"))Dismiss();
  if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){Dismiss();Event.current.Use();}
  GUI.skin=old;
 }
 void OnDestroy(){if(instance!=this)return;GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);GameEvents.onGameSceneLoadRequested.Remove(SceneChanging);GameEvents.onLevelWasLoadedGUIReady.Remove(SceneReady);Hide();RemoveButton();if(icon!=null)Destroy(icon);instance=null;}
}
