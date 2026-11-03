using System;using System.IO;using System.Reflection;using System.Security.Cryptography;using UnityEngine;using KSP.UI.Screens;

public sealed partial class KDAGarage {
 CarEntry deleteRequest;string deleteFingerprint="";
 ShipConstruct savedPathShip;string savedCarPath="",savedCarName="",nativeCarName="";
 static readonly MethodInfo NewEmptyShip=typeof(EditorLogic).GetMethod("OnNewShipConfirm",BindingFlags.Instance|BindingFlags.NonPublic);
 // Match stock onSaveConfirm bookkeeping only after the craft write succeeds.
 // SetBackup must run first: later real edits must still differ from this undo index.
 static void MarkEditorSaved(string path,string name){
  var editor=EditorLogic.fetch;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var type=typeof(EditorLogic);
  type.GetField("undoIndexAtLastSave",flags).SetValue(editor,type.GetField("undoLevel",flags).GetValue(editor));
  type.GetField("vesselNameAtLastSave",flags).SetValue(editor,name);
  type.GetMethod("SetLastSanitizedSaveName",flags).Invoke(editor,null);
  type.GetField("savedCraftPath",flags).SetValue(editor,Path.GetFullPath(path));
 }
 void RememberCarPath(string path){
  var config=ConfigNode.Load(path);savedPathShip=EditorLogic.fetch.ship;savedCarPath=Path.GetFullPath(path);
  nativeCarName=savedPathShip.shipName;savedCarName=config==null?nativeCarName:config.GetValue("ship")??nativeCarName;
  RestoreCarName();KDAEditorCraftNames.Remember(path);
 }
 // Stock loading can strip square brackets from the editor's name. Keep
 // both the source identity and its native alias so saving is not a rename.
 string SuggestedCarName(){return savedPathShip==ship&&(ship.shipName==nativeCarName||ship.shipName==savedCarName)?savedCarName:ship.shipName;}
 void RestoreCarName(){if(savedPathShip!=EditorLogic.fetch.ship)return;EditorLogic.fetch.shipNameField.text=savedCarName;savedPathShip.shipName=savedCarName;}
 string CarSavePath(string directory,string name){
  if(savedPathShip==ship&&name==savedCarName&&!string.IsNullOrEmpty(savedCarPath)){
   string root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
   if(savedCarPath.StartsWith(root,StringComparison.OrdinalIgnoreCase))return savedCarPath;
  }
  return Path.Combine(directory,KDAEditorCraftNames.SaveStem(name,'_',true,directory)+".craft");
 }
 bool IsLoadedCarFile(string path){
  if(EditorLogic.fetch==null||EditorLogic.fetch.ship==null||EditorLogic.fetch.ship.parts.Count==0)return false;
  // Use the actual source file, never a display name: included/save duplicates
  // often share names. The native startup path also covers opening stock SPH.
  string loaded=savedPathShip==EditorLogic.fetch.ship?savedCarPath:(int)EditorDriver.StartupBehaviour==2?EditorDriver.filePathToLoad:"";
  return !string.IsNullOrEmpty(loaded)&&string.Equals(Path.GetFullPath(loaded),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase);
 }
 void ClearDeletedCar(){
  DiscardPreview();if(model!=null){model.localPosition=modelPosition;model.localRotation=modelRotation;}
  if(EditorLogic.fetch.toolsUI!=null)EditorLogic.fetch.toolsUI.SetMode(ConstructionMode.Place);
  if(UIPartActionController.Instance!=null)UIPartActionController.Instance.Deselect(true);
  // Stock New Craft cleanup removes the real parts, crew assignment, undo
  // history and selection. Hiding renderers would leave a savable ghost car.
  NewEmptyShip.Invoke(EditorLogic.fetch,null);EditorDriver.filePathToLoad="";
  savedPathShip=null;savedCarPath=savedCarName=nativeCarName="";saveName="";view=GarageView.Cars;powerGraph=false;
  BindCraft();MaintainGarageCanvases();UpdateNavigationLock();
 }
 static string CraftFingerprint(string path){using(var hash=SHA256.Create())using(var stream=File.OpenRead(path))return Convert.ToBase64String(hash.ComputeHash(stream));}
 bool DeletableCraftPath(string path){
  string full=Path.GetFullPath(path),game=Path.GetFullPath(KSPUtil.ApplicationRootPath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
  if(!string.Equals(Path.GetExtension(full),".craft",StringComparison.OrdinalIgnoreCase)||!File.Exists(full))return false;
  string save=Path.Combine(game,"saves",HighLogic.SaveFolder,"Ships");
  foreach(string root in new[]{Path.Combine(save,"SPH"),Path.Combine(save,"VAB"),Path.Combine(game,"Ships","SPH"),Path.Combine(game,"Ships","VAB")}){
   string folder=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
   if(!folder.StartsWith(game+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!full.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))continue;
   if((File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)return false;
   // A junction in any ancestor must not redirect a saved-car deletion.
   for(var parent=new DirectoryInfo(Path.GetDirectoryName(full));parent!=null;parent=parent.Parent){if((parent.Attributes&FileAttributes.ReparsePoint)!=0)return false;if(string.Equals(parent.FullName.TrimEnd(Path.DirectorySeparatorChar),game,StringComparison.OrdinalIgnoreCase))return true;}
  }
  return false;
 }
 public bool RequestCarDelete(int index){
  if(!open||loadingCar||ModalOpen||HasLoosePart||index<0||index>=cars.Count)return false;
  try{var entry=cars[index];if(!DeletableCraftPath(entry.Path)){error="This saved craft cannot be deleted from the garage.";return false;}deleteFingerprint=CraftFingerprint(entry.Path);deleteRequest=entry;selectedCar=index;error="";return true;}
  catch(Exception ex){error="Could not select saved craft: "+ex.Message;return false;}
 }
 public bool ConfirmCarDelete(){
  if(!open||loadingCar||deleteRequest==null)return false;var entry=deleteRequest;
  try{
   if(!DeletableCraftPath(entry.Path))throw new IOException("The saved craft is missing or its path is no longer valid.");
   if(CraftFingerprint(entry.Path)!=deleteFingerprint)throw new IOException("The saved craft changed. Cancel and select Delete Car again to review it.");
   bool unload=IsLoadedCarFile(entry.Path);if(unload&&NewEmptyShip==null)throw new IOException("This KSP version cannot clear the loaded car safely.");
   File.Delete(entry.Path);
   int removed=cars.IndexOf(entry);if(entry.Preview!=null)Destroy(entry.Preview);cars.Remove(entry);
   if(selectedCar==removed)selectedCar=-1;else if(selectedCar>removed)selectedCar--;
   deleteRequest=null;deleteFingerprint="";error="";
   if(unload){try{ClearDeletedCar();}catch(Exception ex){error="The craft file was deleted, but the editor could not clear it: "+ex.GetBaseException().Message;return true;}}
   status="Deleted saved craft: "+entry.Name+(unload?". Garage empty; choose another car.":". Installed vehicle parts are still available.");return true;
  }catch(Exception ex){error="Could not delete saved craft: "+ex.Message;return false;}
 }
 void DrawDeleteConfirmation(){
  var entry=deleteRequest;GUI.Label(new Rect(450,281,540,42),"DELETE SAVED CAR?",heading);GUI.Label(new Rect(450,329,540,43),entry.Name,label);
  string root=Path.GetFullPath(KSPUtil.ApplicationRootPath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
  string full=Path.GetFullPath(entry.Path);string relative=full.StartsWith(root,StringComparison.OrdinalIgnoreCase)?full.Substring(root.Length):full;
  GUI.Label(new Rect(450,375,540,62),relative,small);
  GUI.Label(new Rect(450,440,540,79),error.Length>0?error:IsLoadedCarFile(entry.Path)?"Permanently deletes this .craft file and unloads the current car, including unsaved changes. The garage will be empty until you load another car.":"Permanently deletes this .craft file. Your currently loaded car and installed vehicle parts remain available.",label);
  if(Button(new Rect(450,529,245,44),"CANCEL",false))CancelModal();
  var old=GUI.backgroundColor;GUI.backgroundColor=new Color(.7f,.22f,.18f);if(GUI.Button(new Rect(717,529,273,44),"DELETE CRAFT FILE",button))ConfirmCarDelete();GUI.backgroundColor=old;
 }
}

// Preserve source identity in both stock editors as well as the KDA garage.
[KSPAddon(KSPAddon.Startup.EditorAny,false)]
public sealed class KDAEditorCraftNames:MonoBehaviour {
 static ShipConstruct sourceShip;static string sourcePath="",sourceName="",nativeName="";static bool patched;
 bool startupChecked;
 void Awake(){
  if(patched)return;
  try{var h=new HarmonyLib.Harmony("kda.editor.craft.identity");
   h.Patch(HarmonyLib.AccessTools.Method(typeof(EditorLogic),"ShipToLoadSelected",new[]{typeof(string),typeof(KSP.UI.Screens.CraftBrowserDialog.LoadType)}),postfix:new HarmonyLib.HarmonyMethod(typeof(KDAEditorCraftNames),"Loaded"));
   h.Patch(HarmonyLib.AccessTools.Method(typeof(ShipConstruction),"GetSavePath",new[]{typeof(string)}),postfix:new HarmonyLib.HarmonyMethod(typeof(KDAEditorCraftNames),"SavePath"));
   h.Patch(HarmonyLib.AccessTools.Method(typeof(ShipConstruction),"SaveShipToPath",new[]{typeof(string),typeof(string)}),transpiler:new HarmonyLib.HarmonyMethod(typeof(KDAEditorCraftNames),"SaveFilename"));
   patched=true;
  }catch(Exception ex){Debug.LogError("[KDA Garage] Craft name compatibility: "+ex);}
 }
 static bool IsCar(ShipConstruct s){if(s==null)return false;foreach(var p in s.parts)if(p!=null&&p.Modules.Contains("KDAEngine"))return true;return false;}
 public static void Remember(string path){
  try{var editor=EditorLogic.fetch;if(editor==null||!IsCar(editor.ship)||!File.Exists(path))return;
   var config=ConfigNode.Load(path);if(config==null)return;sourceShip=editor.ship;sourcePath=Path.GetFullPath(path);nativeName=sourceShip.shipName;sourceName=config.GetValue("ship")??nativeName;
   editor.shipNameField.text=sourceName;sourceShip.shipName=sourceName;
  }catch(Exception ex){Debug.LogWarning("[KDA Garage] Could not retain craft name: "+ex.Message);}
 }
 static void Loaded(string __0,KSP.UI.Screens.CraftBrowserDialog.LoadType __1){if((int)__1==0)Remember(__0);}
 void LateUpdate(){var editor=EditorLogic.fetch;if(startupChecked||editor==null||editor.ship==null||editor.ship.parts.Count==0)return;startupChecked=true;if(sourceShip!=editor.ship&&(int)EditorDriver.StartupBehaviour==2&&!string.IsNullOrEmpty(EditorDriver.filePathToLoad))Remember(EditorDriver.filePathToLoad);}
 static bool Matches(string name){return EditorLogic.fetch!=null&&sourceShip==EditorLogic.fetch.ship&&(name==sourceName||name==nativeName);}
 static string RetainedStem(string name,string directory){
  if(!Matches(name)||string.IsNullOrEmpty(directory))return null;
  var folder=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
  return sourcePath.StartsWith(folder,StringComparison.OrdinalIgnoreCase)?Path.GetFileNameWithoutExtension(sourcePath):null;
 }
 static void SavePath(string __0,ref string __result){if(!HighLogic.LoadedSceneIsEditor)return;string directory=ShipConstruction.GetCurrentGameShipsPathFor(EditorDriver.editorFacility);string stem=SaveStem(__0,'_',true,directory);if(IsCar(EditorLogic.fetch==null?null:EditorLogic.fetch.ship))__result=Path.Combine(directory,stem+".craft");}
 public static string SaveStem(string name,char replacement,bool spaces,string directory){
  if(HighLogic.LoadedSceneIsEditor){string stem=RetainedStem(name,directory);if(stem!=null)return stem;
   // New copies/renames retain valid bracket characters, while still applying
   // the stock sanitizer for invalid filename characters.
   if(IsCar(EditorLogic.fetch==null?null:EditorLogic.fetch.ship)&&name.IndexOfAny(Path.GetInvalidFileNameChars())<0&&name.Trim().Length>0&&name!="."&&name!="..")return name;
  }
  return KSPUtil.SanitizeString(name,replacement,spaces);
 }
 static System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction> SaveFilename(System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction> code){
  var sanitizer=HarmonyLib.AccessTools.Method(typeof(KSPUtil),"SanitizeString",new[]{typeof(string),typeof(char),typeof(bool)});
  foreach(var instruction in code){if(instruction.opcode==System.Reflection.Emit.OpCodes.Call&&Equals(instruction.operand,sanitizer)){
    var load=new HarmonyLib.CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_1);load.labels.AddRange(instruction.labels);instruction.labels.Clear();load.blocks.AddRange(instruction.blocks);instruction.blocks.Clear();yield return load;
    instruction.operand=HarmonyLib.AccessTools.Method(typeof(KDAEditorCraftNames),"SaveStem");
   }yield return instruction;}
 }
}
