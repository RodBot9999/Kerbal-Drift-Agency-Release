using UnityEngine;

// Selection geometry must not become a second ground-contact collider.
public class KDAWheelSelection : PartModule {
 public override void OnStart(StartState state){base.OnStart(state);Apply();}
 void Apply(){
  foreach(var collider in part.GetComponentsInChildren<Collider>(true))
   if(collider.gameObject.name=="kdaSelectionCollider")collider.enabled=HighLogic.LoadedSceneIsEditor;
 }
}
