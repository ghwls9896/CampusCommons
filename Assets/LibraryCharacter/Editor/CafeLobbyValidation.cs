using System;
using System.IO;
using System.Linq;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor {
public static class CafeLobbyValidation {
 const string Result="Tools/cafe-lobby-validation.txt", Pending="CafeLobbyValidationManualV2";
 static Player player;static CharacterController body;static EscapeGame escape;static LobbyDoor[] doors;static int stage;static float at,angle,lastFixed;static bool loggedError;static string textResult;
 [InitializeOnLoadMethod] static void Init(){EditorApplication.delayCall+=Resume;EditorApplication.playModeStateChanged+=OnPlayModeStateChanged;}
 static void Attach(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
 static void Detach(){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;}
 static void Resume(){
  if(!SessionState.GetBool(Pending,false))return;
  if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Pending,false);return;}
  Attach();
 }
 static void OnPlayModeStateChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode){SessionState.SetBool(Pending,false);Detach();}}
 [MenuItem("Campus/Testing/Run lobby checks")]
 public static void Begin(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().path!="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity"||SceneManager.GetActiveScene().isDirty){Debug.LogWarning("Lobby checks require the saved GrandLibrary_Living scene and Play mode to be stopped.");return;}
  try{stage=0;loggedError=false;textResult=CheckTextOcclusion();SessionState.SetString("CafeTextTest",textResult);SessionState.SetBool(Pending,true);Attach();EditorApplication.EnterPlaymode();}
  catch(Exception e){SessionState.SetBool(Pending,false);Detach();File.WriteAllText(Result,"FAIL: "+e);Debug.LogException(e);}
 }
 static void Log(string message,string stack,LogType type){if(stack!=null&&stack.Contains("UnityEditor.Search.SearchDatabase"))return;if(type==LogType.Exception||type==LogType.Error)loggedError=true;}
 static void Check(bool v,string message){if(!v)throw new Exception(message);}
 static void Next(int value){stage=value;at=Time.time;}
 static Vector3 At(float radius,float degrees,float y){float a=degrees*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a)*radius,y,Mathf.Cos(a)*radius);}
 static void Place(float z){body.enabled=false;player.transform.position=new Vector3(12.15f,.08f,z);body.enabled=true;Physics.SyncTransforms();}
 static void CheckSeats(){
  var seats=UnityEngine.Object.FindObjectsByType<Seat>();Check(seats.Length>=50,"Missing seat interaction points");
  foreach(var seat in seats){body.enabled=false;player.transform.position=seat.transform.position-Vector3.up*.6f;body.enabled=true;Physics.SyncTransforms();Check(player.TrySit(seat),"Could not sit: "+seat.name);Check(seat.occupant==player,"Seat did not become occupied");player.Stand();Check(!seat.occupant,"Seat did not clear on standing");}
 }
 static void StartDoor(string name,float z){Place(z);var door=doors.First(d=>d.destinationLabel==name);Check(LobbyDoor.Nearest(player)==door,"Wrong nearest entrance: "+name);Check(door.TryEnter(player),"Door could not be used: "+name);}
 static void ReturnRoom(Portal.Space expected){Check(player.currentSpace==expected,"Wrong room reached");Check(player.RequestRoomExit()&&player.ConfirmRoomExit()&&player.currentSpace==Portal.Space.Library,"Room return failed");}
 static void Tick(){
  if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||Time.timeSinceLevelLoad<1f)return;
  try{
   if(stage==0){
    player=UnityEngine.Object.FindAnyObjectByType<Player>();escape=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();doors=UnityEngine.Object.FindObjectsByType<LobbyDoor>();Check(player&&escape&&doors.Length==3,"Player, escape, or entrances missing");body=player.GetComponent<CharacterController>();Check(body&&body.enabled,"Player controller missing");
    Check(doors.All(d=>d.hinge&&d.hinge.GetComponentInChildren<Collider>()),"A door has no physical panel collider");Check(doors.All(d=>d.room? !d.room.GetComponent<Collider>().enabled : d.escape && !d.escape.GetComponent<Collider>().enabled),"A portal trigger is still active");
    Check(GameObject.Find("Victorian chandelier")&&GameObject.Find("Glass dome")&&GameObject.Find("Cafe Interior"),"A lobby feature is missing");
    var typography=UnityEngine.Object.FindAnyObjectByType<WorldTextFontAtlas>();Check(typography&&typography.font&&typography.depthMaterial,"Modern sign font missing");
    typography.font.RequestCharactersInTexture("ESCAPE LOUNGE 0123456789",29,FontStyle.Bold);Check(typography.depthMaterial.mainTexture==typography.font.material.mainTexture,"Font atlas was not refreshed");
    var schoolTables=GameObject.Find("School Hall Tables");Check(schoolTables&&schoolTables.transform.childCount==12,"Grounded school tables missing");
    Check(schoolTables.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Study items "))==36,"Tabletop items are not attached to the school tables");
    CheckSeats();
    Place(-5.5f);var flags=body.Move(Vector3.right*1.9f);Check((flags&CollisionFlags.Sides)!=0,"Closed door does not block the character");StartDoor("MAFIA",-5.5f);Next(1);
   } else if(stage==1){if(player.currentSpace==Portal.Space.Mafia){ReturnRoom(Portal.Space.Mafia);Next(2);}else Check(Time.time-at<4,"Mafia door failed to enter");}
   else if(stage==2&&Time.time-at>1.6f){StartDoor("QUIZ",0);Next(3);}
   else if(stage==3){if(player.currentSpace==Portal.Space.Quiz){ReturnRoom(Portal.Space.Quiz);Next(4);}else Check(Time.time-at<4,"Quiz door failed to enter");}
   else if(stage==4&&Time.time-at>1.6f){StartDoor("ESCAPE",5.5f);Next(5);}
   else if(stage==5){if(escape.IsSelecting){Check(player.currentSpace==Portal.Space.Escape&&!player.enabled,"Escape selection failed");Check(escape.ChooseMode(EscapeGame.Mode.Easy),"Escape mode start failed");escape.ExitToLobby();Check(player.enabled&&player.currentSpace==Portal.Space.Library,"Escape return failed");player.enabled=false;angle=-152f;lastFixed=-1;body.enabled=false;player.transform.position=At(13.2f,angle,.08f);body.enabled=true;Physics.SyncTransforms();Next(6);}else Check(Time.time-at<4,"Escape door failed to enter");}
   else if(stage==6&&Time.time-at>.5f){
    if(Time.fixedTime>lastFixed){lastFixed=Time.fixedTime;angle=Mathf.Min(270,angle+.9f);var desired=At(13.2f,angle,player.transform.position.y);var delta=desired-player.transform.position;delta.y=-.035f;body.Move(delta);Check(new Vector2(player.transform.position.x-desired.x,player.transform.position.z-desired.z).magnitude<.8f,"Stair route blocked at "+angle+" position="+player.transform.position);}
    if(angle>=59&&angle<61)Check(player.transform.position.y>4.4f,"First stair level not reached");
    if(angle>=270){Check(player.transform.position.y>9.2f,"Top floor not reached");Check(!loggedError,"Unity reported a runtime error");File.WriteAllText(Result,"PASS: "+SessionState.GetString("CafeTextTest","")+"; all seats, modern font atlas, school table props, three doors, destination returns, escape mode, and two-floor staircase.\n");Finish();}Check(Time.time-at<30,"Stair traversal timed out");
   }
  }catch(Exception ex){File.WriteAllText(Result,"FAIL: "+ex);Debug.LogException(ex);Finish();}
 }
 static void Finish(){Detach();SessionState.SetBool(Pending,false);EditorApplication.ExitPlaymode();}
 static string CheckTextOcclusion(){
  var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/LibraryCharacter/Generated/InteriorGroups/Modern world text.mat");Check(mat&&mat.shader.name=="Campus/WorldTextDepth","World text depth material missing");
  var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/StudyImmersive/Resources/Inter-SemiBold.ttf");Check(font,"Modern font missing");font.RequestCharactersInTexture("TEST",64);mat.mainTexture=font.material.mainTexture;
  var cameraObj=new GameObject("Text test camera"){hideFlags=HideFlags.HideAndDontSave,layer=31};cameraObj.transform.position=new Vector3(3000,3000,3000);var cam=cameraObj.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=1.5f;cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.nearClipPlane=.1f;cam.farClipPlane=10;
  var textObj=new GameObject("Text visibility test"){hideFlags=HideFlags.HideAndDontSave,layer=31};textObj.transform.position=cameraObj.transform.position+Vector3.forward*3;var text=textObj.AddComponent<TextMesh>();text.font=font;text.text="TEST";text.fontSize=64;text.characterSize=.07f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;text.GetComponent<Renderer>().sharedMaterial=mat;
  var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.hideFlags=HideFlags.HideAndDontSave;blocker.layer=31;blocker.transform.position=cameraObj.transform.position+Vector3.forward*2;blocker.transform.localScale=new Vector3(3,3,.1f);var blockMat=new Material(Shader.Find("Unlit/Color")){color=new Color(.1f,.35f,.1f)};blocker.GetComponent<Renderer>().sharedMaterial=blockMat;
  var rt=new RenderTexture(128,128,24);cam.targetTexture=rt;var img=new Texture2D(128,128,TextureFormat.RGB24,false);
  Func<Color32[]> capture=()=>{cam.Render();RenderTexture.active=rt;img.ReadPixels(new Rect(0,0,128,128),0,0);img.Apply();return img.GetPixels32();};
  var withBlock=capture();textObj.SetActive(false);var blockOnly=capture();textObj.SetActive(true);blocker.SetActive(false);var visible=capture();textObj.SetActive(false);var empty=capture();
  int blockedDiff=0,visibleDiff=0;for(int i=0;i<visible.Length;i++){if(Math.Abs(withBlock[i].r-blockOnly[i].r)+Math.Abs(withBlock[i].g-blockOnly[i].g)+Math.Abs(withBlock[i].b-blockOnly[i].b)>12)blockedDiff++;if(Math.Abs(visible[i].r-empty[i].r)+Math.Abs(visible[i].g-empty[i].g)+Math.Abs(visible[i].b-empty[i].b)>12)visibleDiff++;}
  cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(img);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(blockMat);UnityEngine.Object.DestroyImmediate(blocker);UnityEngine.Object.DestroyImmediate(textObj);UnityEngine.Object.DestroyImmediate(cameraObj);
  Check(visibleDiff>30,"World text did not render in visibility test: "+visibleDiff);Check(blockedDiff==0,"World text showed through an opaque object: "+blockedDiff);return "text visible="+visibleDiff+", occluded pixels="+blockedDiff;
 }
}}
 


