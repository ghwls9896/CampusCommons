using System;
using System.Collections.Generic;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class EscapeNpcLockedRouteVerification
{
 const string Pending="EscapeNpcLockedRouteVerification";
 static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static object Field(object o,string n)=>o.GetType().GetField(n,flags).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,flags).SetValue(o,v);
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Pending,false))EditorApplication.update+=Tick;};}
 static void Finish(int status){EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);EditorApplication.Exit(status);}
 static Vector3 Ground(Vector3 p){p.y=.08f;return p;}
 static void Place(object npc,Vector3 p){var body=(CharacterController)Field(npc,"body");body.enabled=false;((GameObject)Field(npc,"root")).transform.position=Ground(p);body.enabled=true;Physics.SyncTransforms();}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;try{
 var game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var player=UnityEngine.Object.FindAnyObjectByType<Player>();Check(game.BeginSelection(player)&&game.ChooseMode(EscapeGame.Mode.Easy),"Game start failed");game.monster.enabled=false;var world=game.World;world.enabled=false;
 var fixture=new GameObject("NPC locked door fixture");fixture.SetActive(false);fixture.transform.position=new Vector3(1001,0,1001);var panel=fixture.AddComponent<BoxCollider>();panel.center=Vector3.up;panel.size=new Vector3(.8f,2f,.1f);var door=fixture.AddComponent<Door>();door.hinge=fixture.transform;fixture.SetActive(true);Set(world,"doors",new[]{door});
 var grid=ScriptableObject.CreateInstance<SchoolGrid>();grid.step=.5f;grid.minX=1998;grid.minZ=1998;grid.width=9;grid.height=9;grid.links=new byte[81];for(int z=0;z<9;z++)for(int x=0;x<9;x++){int id=z*9+x;grid.links[id]=(byte)((z<8?1:0)|(x<8?2:0)|(z>0?4:0)|(x>0?8:0));}Set(world,"map",grid);
 var move=typeof(EscapeWorld).GetMethod("MovePatrol",flags);var ambient=typeof(EscapeWorld).GetMethod("UpdatePatrolAmbient",flags);
 foreach(string name in new[]{"professor","worker"}){
  var npc=Field(world,name);Check(npc!=null,name+" missing");var start=new Vector3(1001,0,1000);var goal=new Vector3(1001,0,1002);door.Unlock();Place(npc,start);var route=(List<Vector3>)Field(npc,"route");Check(grid.FindPath(start,goal,route),"Initial route missing");Set(npc,"next",0);Set(npc,"destination",goal);Set(npc,"hasDestination",true);Set(npc,"replan",4f);Check(door.TryLock(15f,null),"Lock failed");var before=((GameObject)Field(npc,"root")).transform.position;move.Invoke(world,new object[]{npc,3.6f,.1f});
  Check((bool)Field(npc,"hasDestination")&&Vector3.Distance((Vector3)Field(npc,"destination"),goal)<.01f,name+" lost reachable goal");Check(route.Count>0,name+" has no detour");var prior=start;foreach(var point in route){Check(!world.IsEdgeBlocked(prior,point),name+" detour crosses locked door");prior=point;}Check(Vector3.Distance(before,((GameObject)Field(npc,"root")).transform.position)>.01f,name+" did not move immediately");
  door.Unlock();Check(!world.IsEdgeBlocked(new Vector3(1001,0,1000.5f),new Vector3(1001,0,1001f)),name+" unlock still blocks path");
  var audio=(AudioSource)Field(npc,"audio");audio.Stop();var other=Field(world,name=="professor"?"worker":"professor");((AudioSource)Field(other,"audio")).Stop();Set(npc,"ambientWait",0f);ambient.Invoke(world,new[]{npc,(object).1f});float wait=(float)Field(npc,"ambientWait");Check(wait>=5.5f && wait<=9f,name+" sound interval wrong: "+wait);
  Debug.Log("NPC_LOCKED_DETOUR_PASS "+name+" points="+route.Count+" soundInterval="+wait);
 }
 // Remove all grid links crossing the door except the direct locked crossing: goal becomes unreachable, but the near side still has patrol destinations.
 for(int x=0;x<9;x++)if(x!=4){grid.links[3*9+x]=(byte)(grid.links[3*9+x]&~1);grid.links[4*9+x]=(byte)(grid.links[4*9+x]&~4);}
 foreach(string name in new[]{"professor","worker"}){
  var npc=Field(world,name);door.Unlock();var start=new Vector3(1001,0,1000);var goal=new Vector3(1001,0,1002);Place(npc,start);var route=(List<Vector3>)Field(npc,"route");Check(grid.FindPath(start,goal,route),"Initial direct path missing");Set(npc,"next",0);Set(npc,"destination",goal);Set(npc,"hasDestination",true);Set(npc,"replan",4f);Check(door.TryLock(15f,null),"Lock failed");move.Invoke(world,new object[]{npc,3.6f,.1f});Check((bool)Field(npc,"hasDestination") && Vector3.Distance((Vector3)Field(npc,"destination"),goal)>1f && route.Count>0,name+" did not choose reachable alternate goal");door.Unlock();Debug.Log("NPC_LOCKED_ALTERNATE_PASS "+name);
 }
 Debug.Log("NPC_LOCKED_ROUTE_PASS two NPCs detour alternate goal unlock and shorter audio interval");Finish(0);
 }catch(Exception e){Debug.LogException(e);Finish(1);}}
}
