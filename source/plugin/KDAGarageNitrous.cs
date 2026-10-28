using UnityEngine;
public sealed partial class KDAGarage {
 bool pickingNitrousKey;
 void BindNitrous(){
  if(body==null)return;var e=body.FindModuleImplementing<KDAEngine>();if(e==null)return;
  AddExtra(e,"nitrousKit","Nitrous kit","",0,1,1,"");AddExtra(e,"nitrousShotHp","Shot size","hp",25,300,25,"Added crank horsepower while spraying.");
  AddExtra(e,"nitrousBottle","Bottle size","",0,2,1,"");AddExtra(e,"nitrousKey","Activation key","",0,329,1,"");
  foreach(string n in new[]{"nitrousKit","nitrousBottle","nitrousKey"}){var s=Find(false,n);if(s!=null)s.Integer=true;}
  pickingNitrousKey=false;
 }
 bool CaptureNitrousKey(){
  if(!pickingNitrousKey||view!=GarageView.Tune||section!=6||Event.current.type!=EventType.KeyDown)return false;
  var key=Event.current.keyCode;if(key==KeyCode.Escape){pickingNitrousKey=false;Event.current.Use();return true;}
  if(key!=KeyCode.None&&(int)key<(int)KeyCode.Mouse0){var s=Find(false,"nitrousKey");if(s!=null)Set(s,(int)key,true);pickingNitrousKey=false;Event.current.Use();return true;}return false;
 }
 void DrawNitrous(){
  GUI.BeginGroup(new Rect(24,310,406,474));GUI.Label(new Rect(4,0,375,28),"RECHARGING NITROUS",sectionStyle);
  var kit=Find(false,"nitrousKit");var bottle=Find(false,"nitrousBottle");var shot=Find(false,"nitrousShotHp");var key=Find(false,"nitrousKey");if(kit==null||bottle==null||shot==null||key==null){GUI.EndGroup();return;}
  for(int i=0;i<2;i++)if(Button(new Rect(4+i*190,35,180,36),i==0?"OFF":"INSTALLED",(int)kit.Value==i))Set(kit,i,true);
  Row(shot,84);GUI.Label(new Rect(4,182,375,25),"BOTTLE SIZE",sectionStyle);
  string[] names={"SMALL","MEDIUM","LARGE"};for(int i=0;i<3;i++)if(Button(new Rect(4+i*126,215,118,36),names[i],(int)bottle.Value==i))Set(bottle,i,true);
  double seconds=KerbalDriftAgency.NitrousCore.Capacity((int)bottle.Value)*100/Mathf.Max(25,shot.Value);
  GUI.Label(new Rect(4,259,375,43),"About "+seconds.ToString("0.#")+" seconds of spray at this shot size.",small);
  if(Button(new Rect(4,304,370,37),pickingNitrousKey?"PRESS A KEY / ESC TO CANCEL":"HOLD KEY: "+((KeyCode)(int)key.Value).ToString()+"  /  CHANGE",pickingNitrousKey)){pickingNitrousKey=!pickingNitrousKey;GUI.FocusControl("");GUIUtility.keyboardControl=0;}
  GUI.Label(new Rect(4,352,375,110),"Hold while accelerating in Drive above 1500 rpm. Release to recharge. Grounded drifts above 15 km/h recharge 3x faster.\n\nApply, then Save Car. You can also bind the engine's Nitrous (hold key) action in KSP.",small);GUI.EndGroup();
 }
}
