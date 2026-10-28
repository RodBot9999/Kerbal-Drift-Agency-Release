using System.IO;using UnityEngine;

public sealed partial class KDAGarage {
 Texture2D sparky;GUIStyle brandHeading,brandCredit;
 void DrawGarageBrand(){
  if(brandHeading==null){
   brandHeading=new GUIStyle(heading){fontSize=23,wordWrap=false};brandCredit=new GUIStyle(small){fontSize=13};brandCredit.normal.textColor=new Color(.30f,.95f,.48f,1);
   using(var stream=typeof(KDAGarage).Assembly.GetManifestResourceStream("KDA.Sparky.png"))if(stream!=null){using(var bytes=new MemoryStream()){stream.CopyTo(bytes);sparky=new Texture2D(2,2,TextureFormat.RGBA32,false);sparky.LoadImage(bytes.ToArray());sparky.name="KDA Sparky logo";sparky.wrapMode=TextureWrapMode.Clamp;sparky.filterMode=FilterMode.Bilinear;}}
  }
  if(sparky!=null)GUI.DrawTexture(new Rect(23,7,81,81),sparky,ScaleMode.ScaleToFit,true);
  GUI.Label(new Rect(110,8,421,34),"KERBAL DRIFT AGENCY",brandHeading);
  GUI.Label(new Rect(112,41,410,23),"by Roderic Systems",brandCredit);
  GUI.Label(new Rect(112,65,414,25),"GARAGE   /   "+(loadingCar?loadingName:ship==null||ship.parts.Count==0?"Choose your car":ship.shipName),small);
 }
}
