using System.Collections.Generic;using UnityEngine;using UnityEngine.UI;using UnityEngine.EventSystems;using KSP.UI;using KSP.UI.Screens;

// Native icon models are not canvas graphics. Keep them hidden separately,
// and suppress raycasters as well as visuals while IMGUI owns the garage.
public sealed partial class KDAGarage {
 readonly Dictionary<GameObject,bool> hiddenIcons=new Dictionary<GameObject,bool>();
 readonly Dictionary<GraphicRaycaster,bool> hiddenRaycasters=new Dictionary<GraphicRaycaster,bool>();
 GameObject inputShield;
 public bool StockPointerBlocked(Vector2 mouse){return open&&(loadingCar||ModalOpen||editingText||view!=GarageView.Build||!new Rect(CarRect.x,CarRect.y+108,CarRect.width,CarRect.height-170).Contains(mouse));}
 void MaintainGarageInput(){
  if(inputShield==null){inputShield=new GameObject("KDA Garage pointer shield",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));var canvas=inputShield.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
   var panel=new GameObject("KDA panel hit area",typeof(RectTransform),typeof(Image),typeof(KDAGarageInputShield));panel.transform.SetParent(inputShield.transform,false);var rect=panel.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;panel.GetComponent<Image>().color=Color.clear;panel.GetComponent<KDAGarageInputShield>().Garage=this;
  }
  foreach(var saved in hiddenCanvas)if(saved.Canvas!=null)foreach(var ray in saved.Canvas.GetComponentsInChildren<GraphicRaycaster>(true)){if(!hiddenRaycasters.ContainsKey(ray))hiddenRaycasters.Add(ray,ray.enabled);ray.enabled=false;}
  var list=EditorPartList.Instance;if(list!=null)foreach(var icon in list.GetComponentsInChildren<EditorPartIcon>(true))if(icon.PartIcon!=null){var obj=icon.PartIcon.gameObject;if(!hiddenIcons.ContainsKey(obj))hiddenIcons.Add(obj,obj.activeSelf);obj.SetActive(false);}
  var ui=UIMasterController.Instance;if(ui!=null&&ui.CurrentTooltip!=null){string name=ui.CurrentTooltip.GetType().Name;if(name.Contains("PartList")||name.Contains("EditorPartIcon"))ui.DestroyCurrentTooltip();}
 }
 void ReleaseGarageInput(){
  if(inputShield!=null){inputShield.SetActive(false);Destroy(inputShield);inputShield=null;}
  foreach(var pair in hiddenRaycasters)if(pair.Key!=null)pair.Key.enabled=pair.Value;hiddenRaycasters.Clear();
  foreach(var pair in hiddenIcons)if(pair.Key!=null)pair.Key.SetActive(pair.Value);hiddenIcons.Clear();
 }
}
public sealed class KDAGarageInputShield:MonoBehaviour,ICanvasRaycastFilter {
 public KDAGarage Garage;
 public bool IsRaycastLocationValid(Vector2 point,Camera camera){return Garage!=null&&Garage.StockPointerBlocked(new Vector2(point.x*1440f/Screen.width,(Screen.height-point.y)*900f/Screen.height));}
}
