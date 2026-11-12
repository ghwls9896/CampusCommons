using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class EscapeOpenDoorRouteVerification {
 const string Key="EscapeOpenDoorRouteVerification"; static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object Field(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod] static void Init(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;SessionState.SetBool(Key,false);try{
  var game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var player=UnityEngine.Object.FindAnyObjectByType<Player>();Check(game.BeginSelection(player)&&game.ChooseMode(EscapeGame.Mode.Easy),"start failed");var world=game.World;world.enabled=false;
  var professor=Field(world,"professor");var worker=Field(world,"worker");var pb=(CharacterController)Field(professor,"body");var wb=(CharacterController)Field(worker,"body");
  Check(Physics.GetIgnoreCollision(pb,wb),"patrols can physically block one another");game.monster.SpeedUp();Check(Physics.GetIgnoreCollision(pb,game.monster.GetComponent<CharacterController>()),"monster and patrol can physically block one another");
  var doorObject=new GameObject("open leaf fixture");doorObject.SetActive(false);doorObject.transform.position=new Vector3(1000,0,1000);var panel=doorObject.AddComponent<BoxCollider>();panel.center=new Vector3(.5f,1,0);panel.size=new Vector3(1,2,.1f);var door=doorObject.AddComponent<Door>();door.hinge=doorObject.transform;doorObject.SetActive(true);door.OpenForMonster();doorObject.transform.localRotation=Quaternion.Euler(0,door.openAngle,0);Physics.SyncTransforms();Check(door.IsFullyOpen,"fixture did not open");typeof(EscapeWorld).GetField("doors",Flags).SetValue(world,new[]{door});var center=panel.bounds.center;var a=new Vector3(center.x-.6f,0,center.z);var b=new Vector3(center.x+.6f,0,center.z);var method=typeof(EscapeWorld).GetMethod("CanTraverse",Flags);Check(!(bool)method.Invoke(world,new object[]{a,b}),"open door leaf was not treated as an obstacle");Check((bool)method.Invoke(world,new object[]{new Vector3(center.x+2,0,center.z),new Vector3(center.x+2.5f,0,center.z)}),"unrelated edge blocked");
  var monster=game.monster;typeof(Monster).GetField("doors",Flags).SetValue(monster,new[]{door});var monsterTraverse=typeof(Monster).GetMethod("CanTraverse",Flags);Check(!(bool)monsterTraverse.Invoke(monster,new object[]{a,b}),"monster ignores the open leaf");
  Check((bool)method.Invoke(world,new object[]{new Vector3(center.x+.05f,0,center.z),new Vector3(center.x+.6f,0,center.z)}),"actor touching a leaf cannot step away");
  var grid=ScriptableObject.CreateInstance<SchoolGrid>();grid.step=.5f;grid.minX=1996;grid.minZ=1996;grid.width=9;grid.height=9;grid.links=new byte[81];for(int z=0;z<9;z++)for(int x=0;x<9;x++){int id=z*9+x;grid.links[id]=(byte)((z<8?1:0)|(x<8?2:0)|(z>0?4:0)|(x>0?8:0));}
  var route=new System.Collections.Generic.List<Vector3>();Vector3 start=new Vector3(999,0,999.5f),goal=new Vector3(1001,0,999.5f);Func<Vector3,Vector3,bool> can=(x,y)=>(bool)method.Invoke(world,new object[]{x,y});Check(grid.FindPath(start,goal,route,can),"open leaf prevented an available detour");var prior=start;foreach(var step in route){Check(can(prior,step),"route crosses the open leaf");prior=step;}Check(route.Count>4,"route did not detour around leaf");
  
  typeof(EscapeWorld).GetField("map",Flags).SetValue(world,grid);
  var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="route fixture floor";floor.transform.position=new Vector3(1000,-.1f,1000);floor.transform.localScale=new Vector3(8,.2f,8);
  var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="route fixture wall";wall.transform.position=new Vector3(999.6f,1f,1001);wall.transform.localScale=new Vector3(.1f,2f,1f);Physics.SyncTransforms();
  var move=typeof(EscapeWorld).GetMethod("MovePatrol",Flags);
  foreach(var patrol in new[]{professor,worker}){
    var body=(CharacterController)Field(patrol,"body");body.enabled=false;body.transform.position=new Vector3(999,.08f,1001);body.enabled=true;Physics.SyncTransforms();
    var routeList=(System.Collections.Generic.List<Vector3>)Field(patrol,"route");routeList.Clear();routeList.Add(new Vector3(1001,0,1001));var t=patrol.GetType();t.GetField("next",Flags).SetValue(patrol,0);t.GetField("hasDestination",Flags).SetValue(patrol,true);t.GetField("destination",Flags).SetValue(patrol,new Vector3(1001,0,1001));t.GetField("replan",Flags).SetValue(patrol,20f);t.GetField("stuckTime",Flags).SetValue(patrol,0f);
    bool changed=false;for(int i=0;i<20;i++){move.Invoke(world,new object[]{patrol,4f,.1f});if(Vector3.Distance((Vector3)Field(patrol,"destination"),new Vector3(1001,0,1001))>.2f){changed=true;break;}}Check(changed,"patrol failed to choose a new destination after being blocked");
  }
  
  monster.map=grid;var mb=monster.GetComponent<CharacterController>();mb.enabled=false;mb.transform.position=new Vector3(999,.08f,1001);mb.enabled=true;Physics.SyncTransforms();
  var mp=(System.Collections.Generic.List<Vector3>)Field(monster,"path");mp.Clear();mp.Add(new Vector3(1001,0,1001));var mt=typeof(Monster);mt.GetField("next",Flags).SetValue(monster,0);mt.GetField("hasGoal",Flags).SetValue(monster,true);mt.GetField("goal",Flags).SetValue(monster,new Vector3(1001,0,1001));mt.GetField("stuckTime",Flags).SetValue(monster,0f);mt.GetField("blockedUntil",Flags).SetValue(monster,0f);mt.GetField("waitingDoor",Flags).SetValue(monster,null);
  var moveMonster=mt.GetMethod("MoveAlongPath",Flags);bool blocked=false;for(int i=0;i<25;i++){moveMonster.Invoke(monster,new object[]{.1f});if((float)Field(monster,"blockedUntil")>Time.time){blocked=true;break;}}Check(blocked,"monster did not recover from being stuck");Check(!(bool)monsterTraverse.Invoke(monster,new[]{Field(monster,"blockedFrom"),Field(monster,"blockedTo")}),"monster did not temporarily avoid stuck edge");Debug.Log("MONSTER_STUCK_RECOVERY_PASS");
  Debug.Log("PATROL_STUCK_RECOVERY_PASS professor and worker");
  Debug.Log("OPEN_DOOR_ROUTE_PASS two patrols, monster, leaf avoidance, moving away and available detour");EditorApplication.Exit(0);
 }catch(Exception ex){Debug.LogError("OPEN_DOOR_ROUTE_FAIL "+ex);EditorApplication.Exit(1);}}
}

