using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class EscapePlacementVerification
{
 const string Pending="EscapePlacementVerificationPending"; static int round; static Vector3[] machinePositions; static float start; static EscapeGame game; static Player player;
 static void Check(bool ok,string m){if(!ok)throw new Exception(m);}
 static object Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance).GetValue(o);
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Pending,false))EditorApplication.update+=Tick;};}
 static void Finish(int status){EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);EditorApplication.Exit(status);}
 static void Tick(){if(!EditorApplication.isPlaying)return;try{
 if(Time.timeSinceLevelLoad<2)return;
 if(!game){game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();player=UnityEngine.Object.FindAnyObjectByType<Player>();Check(game&&player,"Game missing");}
 if(round>0 && Time.time-start<1.6f)return;
 Check(game.BeginSelection(player),"Begin selection failed");Check(game.ChooseMode(round%2==0?EscapeGame.Mode.Easy:EscapeGame.Mode.Hard),"Choose mode failed");
 var school=game.schoolRoot.Find("Escape School");Check(school,"School missing");
 var desks=new[]{new Vector3(-12.25f,.788f,35f),new Vector3(10.5f,.788f,35f),new Vector3(-10.5f,.788f,16.25f),new Vector3(-10f,.788f,1.25f),new Vector3(14.75f,.788f,-12.5f)};
 foreach(var p in game.PendingSheetPositions){var local=school.InverseTransformPoint(p);var rooms=new[]{new Vector3(32f,0,-26f),new Vector3(22.5f,0,0f)};Check(desks.Any(d=>Vector3.Distance(local,d)<.6f)||rooms.Any(d=>Vector3.Distance(local,d)<.1f),"Sheet is outside an approved room: "+p);var path=new System.Collections.Generic.List<Vector3>();Check(game.schoolGrid.FindPath(game.schoolArrival,p,path),"Sheet room is unreachable: "+p);}
 var world=game.World;Check(world,"World missing");var professor=GameObject.Find("Professor");Check(professor && professor.GetComponentInChildren<SpriteRenderer>(),"Professor missing or invisible");Check(Vector3.Distance(professor.transform.position,player.transform.position)<=12.1f,"Professor too far from start");
 var machine=GameObject.Find("Soda vending machine");Check(machine,"Machine missing");var front=machine.transform.Find("Photographic vending front");Check(front && front.GetComponent<Renderer>().sharedMaterial.mainTexture,"Photo missing");var logo=machine.transform.Find("KIT university logo");Check(logo && logo.GetComponent<Renderer>().sharedMaterial.mainTexture,"KIT logo missing");
 var list=(IList)Field(world,"pickups");Check(list.Count==5,"Wrong number of pickups: "+list.Count);
 var positions=list.Cast<object>().Select(x=>((GameObject)Field(x,"root")).transform.position).ToArray();for(int i=0;i<positions.Length;i++)for(int j=i+1;j<positions.Length;j++)Check(Vector3.Distance(positions[i],positions[j])>=4.99f,"Pickups too close");
 var machines=world.GetComponentsInChildren<Transform>().Where(x=>x.name=="Soda vending machine").ToArray();Check(machines.Length==3,"Expected three vending machines: "+machines.Length);
 var current=machines.Select(x=>x.position).ToArray();if(machinePositions==null)machinePositions=current;else for(int i=0;i<3;i++)Check(Vector3.Distance(current[i],machinePositions[i])<.01f,"Vending position changed");
 var body=player.GetComponent<CharacterController>();foreach(var m in machines){body.enabled=false;player.transform.position=m.position;body.enabled=true;Physics.SyncTransforms();typeof(EscapeWorld).GetField("coins",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(world,1);Check(world.TryInteract(false)&&world.Coins==0,"Vending purchase failed at "+m.position);bool removed=false;for(int slot=0;slot<3;slot++)if(game.ItemAt(slot)==EscapeGame.InventoryItem.Soda){game.RemoveItem(slot);removed=true;break;}Check(removed,"Soda missing");}
 Debug.Log("ESCAPE_PLACEMENT_ROUND_PASS "+round+" professor="+professor.transform.position+" machine="+machine.transform.position+" sheets="+game.ActiveNotebookCount+" pickups="+list.Count);
 game.ExitToLobby();round++;start=Time.time;if(round==4){Debug.Log("ESCAPE_PLACEMENT_PASS 4 fresh rounds");Finish(0);}
 }catch(Exception e){Debug.LogException(e);Finish(1);}}
}
