using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class InventoryIntegrationChecks
{
 const string Key="InventoryIntegrationChecks";
 static double deadline; static int stage; static Player player; static LibraryInventoryUI inventory;
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(!SessionState.GetBool(Key,false))return;stage=0;deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;};}
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Tick(){try{
  if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Inventory checks timed out");
  if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;
  if(stage==0){
   player=UnityEngine.Object.FindAnyObjectByType<Player>();Check(player,"Player missing");
   inventory=player.GetComponent<LibraryInventoryUI>();Check(inventory,"Inventory missing");
   typeof(LibraryInventoryUI).GetProperty("IsOpen").SetValue(inventory,true);
   player.enabled=false;inventory.SendMessage("Update");
   Check(!inventory.IsOpen,"Inventory stays open when account/room disables Player");
   player.enabled=true;
   inventory.SetOpen(true);Check(inventory.OwnsInput,"Inventory did not acquire lobby input");
   inventory.SelectHotbar(4);Check(inventory.SelectedHotbar==4,"Fifth hotbar cannot be selected");
   foreach(var room in new[]{Portal.Space.Quiz,Portal.Space.Escape,Portal.Space.Mafia}) {
    player.currentSpace=room;inventory.SendMessage("Update");
    Check(!inventory.IsOpen&&!inventory.CanUseInput,"Room UI did not revoke inventory input: "+room);
    inventory.SetOpen(true);inventory.SelectHotbar(1);
    Check(!inventory.IsOpen&&inventory.SelectedHotbar==4,"Blocked inventory handled input");
   }
   player.currentSpace=Portal.Space.Library;
   inventory.SetOpen(true);inventory.enabled=false;Check(!inventory.IsOpen,"Disabled inventory kept preview open");inventory.enabled=true;
#if CAMPUS_SUNBOX_AVATAR
   var customizer=player.GetComponent<LibraryAvatarCustomizerUI>();Check(customizer,"Sunbox customizer missing");
   customizer.Toggle();Check(customizer.IsOpen,"Customizer did not open");
   inventory.SetOpen(true);Check(!inventory.IsOpen,"Inventory overlapped customizer");customizer.Close();
   player.SetFirstPerson(true);inventory.SetOpen(true);stage=1;return;
#else
   Finish(true);
#endif
  }
#if CAMPUS_SUNBOX_AVATAR
  if(stage==1){
   var preview=player.GetComponent<LibraryAvatarPreview>();
   if(!preview||!preview.PreviewTexture||!preview.PreviewTexture.IsCreated())return;
   Check(!player.IsFirstPerson,"Inventory preview kept avatar hidden in first person");
   Check(!player.GetComponent<LibraryAvatarCustomizerUI>().IsOpen,"Inventory needed customizer open");
   var camera=GameObject.Find("Avatar Preview Camera").GetComponent<Camera>();Check(camera.enabled,"Inventory preview camera disabled");
   inventory.SetOpen(false);stage=2;return;
  }
  if(stage==2){
   var camera=GameObject.Find("Avatar Preview Camera").GetComponent<Camera>();
   if(camera.enabled)return;
   player.GetComponent<LibraryAvatarCustomizerUI>().Toggle();stage=3;return;
  }
  if(stage==3){
   var camera=GameObject.Find("Avatar Preview Camera").GetComponent<Camera>();if(!camera.enabled)return;
   Check(!inventory.IsOpen,"Customizer re-opened inventory");Finish(true);
  }
#endif
 }catch(Exception e){Debug.LogException(e);Finish(false);}}
 static void Finish(bool passed){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Debug.Log(passed?"INVENTORY_INTEGRATION_PASS":"INVENTORY_INTEGRATION_FAIL");EditorApplication.Exit(passed?0:1);}
}
