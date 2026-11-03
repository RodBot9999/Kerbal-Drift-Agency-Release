using System;using System.Reflection;using UnityEngine;using UnityEngine.EventSystems;

public sealed partial class KDAGarage {
 GameObject gradeObject;ScriptableObject gradeProfile,gradeSettings;Component gradeVolume;bool cinematic=true,gradeAttempted;
 public bool CinematicAvailable {get{return gradeVolume!=null;}}
 public bool CinematicEnabled {get{return cinematic&&CinematicAvailable;}}
 public void SetCinematic(bool enabled){cinematic=enabled;if(enabled)EnsureGarageGrade();if(gradeVolume!=null)gradeVolume.GetType().GetField("weight").SetValue(gradeVolume,enabled?1f:0f);}
 void EnsureGarageGrade(){
  if(gradeAttempted||editorCamera==null)return;gradeAttempted=true;
  try{
   Type profileType=Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessProfile, TUFX"),volumeType=Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessVolume, TUFX"),colorType=Type.GetType("UnityEngine.Rendering.PostProcessing.ColorGrading, TUFX"),layerType=Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessLayer, TUFX");
   if(profileType==null||volumeType==null||colorType==null||layerType==null)return;var layer=editorCamera.GetComponent(layerType);if(layer==null)return;int mask=((LayerMask)layerType.GetField("volumeLayer").GetValue(layer)).value;int index=0;while(index<32&&(mask&(1<<index))==0)index++;if(index==32)return;
   gradeProfile=ScriptableObject.CreateInstance(profileType);gradeSettings=(ScriptableObject)profileType.GetMethod("AddSettings",new[]{typeof(Type)}).Invoke(gradeProfile,new object[]{colorType});
   SetGradeParameter("enabled",true);SetGradeParameter("gradingMode","HighDefinitionRange");SetGradeParameter("tonemapper","ACES");SetGradeParameter("temperature",-4f);SetGradeParameter("contrast",12f);SetGradeParameter("saturation",4f);SetGradeParameter("postExposure",.10f);
   gradeObject=new GameObject("KDA Garage cinematic look");gradeObject.layer=index;gradeVolume=gradeObject.AddComponent(volumeType);volumeType.GetField("sharedProfile").SetValue(gradeVolume,gradeProfile);volumeType.GetField("isGlobal").SetValue(gradeVolume,true);volumeType.GetField("priority").SetValue(gradeVolume,10000f);volumeType.GetField("weight").SetValue(gradeVolume,cinematic?1f:0f);
  }catch(Exception ex){Debug.LogWarning("[KDA Garage] Cinematic filter unavailable: "+ex.Message);ReleaseGarageGrade();gradeAttempted=true;}
 }
 void SetGradeParameter(string name,object value){var field=gradeSettings.GetType().GetField(name);if(field==null)return;object parameter=field.GetValue(gradeSettings);var vf=parameter.GetType().GetField("value");var of=parameter.GetType().GetField("overrideState");if(vf==null||of==null)return;if(vf.FieldType.IsEnum)value=Enum.Parse(vf.FieldType,(string)value);vf.SetValue(parameter,value);of.SetValue(parameter,true);}
 void ReleaseGarageGrade(){if(gradeObject!=null){gradeObject.SetActive(false);Destroy(gradeObject);}if(gradeSettings!=null)Destroy(gradeSettings);if(gradeProfile!=null)Destroy(gradeProfile);gradeObject=null;gradeVolume=null;gradeSettings=gradeProfile=null;gradeAttempted=false;}
 void DrawGarageTools(){
  Rect car=CarRect;if(Button(new Rect(car.x+330,car.y+16,154,32),CinematicAvailable?(cinematic?"LOOK: CINEMATIC":"LOOK: ORIGINAL"):"LOOK: TUFX NEEDED",CinematicEnabled))SetCinematic(!cinematic);
  if(view!=GarageView.Build)return;
  Box(new Rect(car.x+12,car.y+58,Mathf.Min(900,car.width-24),47),PanelColor);
  string[] names={"1  PLACE","2  MOVE","3  ROTATE","4  ROOT"};for(int i=0;i<4;i++)if(Button(new Rect(car.x+20+i*117,car.y+65,109,32),names[i],EditorLogic.fetch.EditorConstructionMode==(ConstructionMode)i))SelectBuildTool((ConstructionMode)i);
  if(Button(new Rect(car.x+496,car.y+65,105,32),"LOCAL / WORLD",false)&&EditorLogic.fetch.coordSpaceBtn!=null){BeginBuildCommand();EditorLogic.fetch.coordSpaceBtn.onClick.Invoke();}
  if(Button(new Rect(car.x+609,car.y+65,105,32),"SYMMETRY",false)){BeginBuildCommand();EditorLogic.fetch.symButton(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});}
  if(Button(new Rect(car.x+722,car.y+65,105,32),"ANGLE SNAP",false)){BeginBuildCommand();EditorLogic.fetch.snapButton(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});}
 }
}
